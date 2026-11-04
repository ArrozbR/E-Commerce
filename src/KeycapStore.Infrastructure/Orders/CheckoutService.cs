using KeycapStore.Application.Orders;
using KeycapStore.Application.Payments;
using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Payments;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class CheckoutService(
    AppDbContext db,
    TimeProvider clock,
    IPaymentGateway payments,
    IShippingQuotes shippingQuotes,
    IOptions<StripeOptions> stripe) : ICheckoutService
{
    public async Task<ShippingOptionsResult> QuoteShippingAsync(string customerId, string postalCode, CancellationToken cancellationToken = default)
    {
        var shippingItems = await ShippingItemsAsync(customerId, cancellationToken);
        if (shippingItems.Count == 0)
        {
            return ShippingOptionsResult.Fail(PlaceOrderFailure.EmptyCart);
        }

        if (await HasOpenOrderAsync(customerId, cancellationToken))
        {
            return ShippingOptionsResult.Fail(PlaceOrderFailure.OpenOrderExists);
        }

        var quote = await shippingQuotes.QuoteAsync(postalCode, shippingItems, cancellationToken);
        if (!quote.Available)
        {
            return ShippingOptionsResult.Fail(PlaceOrderFailure.ShippingUnavailable);
        }

        if (quote.Options.Count == 0)
        {
            return ShippingOptionsResult.Fail(PlaceOrderFailure.NoShippingOption);
        }

        return ShippingOptionsResult.Success(quote.Options);
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(
        string customerId,
        ShippingAddress shippingAddress,
        int shippingServiceId,
        string language = Order.DefaultLanguage,
        CancellationToken cancellationToken = default)
    {
        var shippingItems = await ShippingItemsAsync(customerId, cancellationToken);
        if (shippingItems.Count == 0)
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.EmptyCart);
        }

        if (await HasOpenOrderAsync(customerId, cancellationToken))
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.OpenOrderExists);
        }

        var quote = await shippingQuotes.QuoteAsync(shippingAddress.PostalCode, shippingItems, cancellationToken);
        if (!quote.Available)
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.ShippingUnavailable);
        }

        var chosen = quote.Options.FirstOrDefault(o => o.ServiceId == shippingServiceId);
        if (chosen is null)
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.ShippingOptionGone);
        }

        var shipping = new ShippingChoice($"{chosen.Carrier} {chosen.Service}", chosen.Price, chosen.DeliveryDays);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var cart = await db.Carts.SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.EmptyCart);
        }

        if (!SameItems(shippingItems, cart.Items))
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.CartChanged);
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var overLimit = cart.Items.FirstOrDefault(i => i.Quantity > Cart.MaxQuantityPerItem);
        if (overLimit is not null)
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.QuantityLimit, products[overLimit.ProductId].Name);
        }

        var preOrder = products.Values.Select(p => p.PreOrder).FirstOrDefault(p => p is not null);
        if (preOrder is not null && products.Count > 1)
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.PreOrderMustBeAlone);
        }

        var now = clock.GetUtcNow();

        foreach (var item in cart.Items.OrderBy(i => i.ProductId))
        {
            var reserved = await db.Products
                .Where(p => p.Id == item.ProductId && p.Stock >= item.Quantity && p.IsActive && (p.PreOrderEndsAt == null || p.PreOrderEndsAt > now))
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity), cancellationToken);

            if (reserved == 0)
            {
                var product = products[item.ProductId];
                var failure = !product.IsActive ? PlaceOrderFailure.NoLongerForSale
                    : product.PreOrder is { } campaign && !campaign.IsOpenAt(now) ? PlaceOrderFailure.PreOrderClosed
                    : PlaceOrderFailure.OutOfStock;
                return PlaceOrderResult.Fail(failure, product.Name);
            }
        }

        var items = cart.Items.Select(i => new OrderItem(i.ProductId, products[i.ProductId].Name, products[i.ProductId].Price, i.Quantity));
        var order = new Order(customerId, now, items, shippingAddress, shipping, language, preOrder?.ShipsAt);

        db.Orders.Add(order);
        db.Carts.Remove(cart);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: OrderConfiguration.OpenOrderPerCustomerIndex })
        {
            db.ChangeTracker.Clear();
            return PlaceOrderResult.Fail(PlaceOrderFailure.OpenOrderExists);
        }

        await transaction.CommitAsync(cancellationToken);

        return PlaceOrderResult.Success(order.Id);
    }

    public async Task<StartPaymentResult> StartPaymentAsync(Guid orderId, string customerId, string customerEmail, Uri returnUrl, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);
        if (order is null)
        {
            return StartPaymentResult.Failure("Pedido não encontrado.");
        }

        CheckoutSession session;
        try
        {
            session = await payments.CreateCheckoutSessionAsync(order, customerEmail, returnUrl, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            order.MarkFailed();
            await db.ReleaseStockAsync(order, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return StartPaymentResult.Failure("Não foi possível iniciar o pagamento. Tente de novo em alguns minutos.");
        }

        order.AttachStripeSession(session.Id);
        await db.SaveChangesAsync(cancellationToken);

        return StartPaymentResult.Success();
    }

    public async Task<PaymentForm?> GetPaymentFormAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);

        if (order is null || order.Status is not OrderStatus.AwaitingPayment || order.StripeSessionId is null)
        {
            return null;
        }

        CheckoutSessionState session;
        try
        {
            session = await payments.GetCheckoutSessionAsync(order.StripeSessionId!, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            return null;
        }

        if (session.ClientSecret is null || session.Status is not "open")
        {
            return null;
        }

        return new PaymentForm(order.Id, session.ClientSecret, stripe.Value.PublishableKey, order.Language);
    }

    private Task<bool> HasOpenOrderAsync(string customerId, CancellationToken cancellationToken) =>
        db.Orders.AnyAsync(
            o => o.CustomerId == customerId && (o.Status == OrderStatus.AwaitingPayment || o.Status == OrderStatus.AwaitingConfirmation),
            cancellationToken);

    private static bool SameItems(IEnumerable<ShippingQuoteItem> quoted, IEnumerable<CartItem> current) =>
        quoted.Select(i => (i.ProductId, i.Quantity)).Order()
            .SequenceEqual(current.Select(i => (i.ProductId, i.Quantity)).Order());

    private async Task<List<ShippingQuoteItem>> ShippingItemsAsync(string customerId, CancellationToken cancellationToken)
    {
        var cart = await db.Carts.AsNoTracking().SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is null)
        {
            return [];
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        return [.. cart.Items.Select(i => new ShippingQuoteItem(i.ProductId, products[i.ProductId].Package, products[i.ProductId].Price, i.Quantity))];
    }
}
