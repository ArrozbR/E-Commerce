using KeycapStore.Application.Orders;
using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Payments;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class CheckoutService(
    AppDbContext db,
    TimeProvider clock,
    IPaymentGateway payments,
    IOptions<StripeOptions> stripe) : ICheckoutService
{
    public async Task<PlaceOrderResult> PlaceOrderAsync(
        string customerId,
        ShippingAddress shippingAddress,
        string language = Order.DefaultLanguage,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var cart = await db.Carts.SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return PlaceOrderResult.Fail(PlaceOrderFailure.EmptyCart);
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var item in cart.Items.OrderBy(i => i.ProductId))
        {
            var reserved = await db.Products
                .Where(p => p.Id == item.ProductId && p.Stock >= item.Quantity && p.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity), cancellationToken);

            if (reserved == 0)
            {
                var product = products[item.ProductId];
                return PlaceOrderResult.Fail(
                    product.IsActive ? PlaceOrderFailure.OutOfStock : PlaceOrderFailure.NoLongerForSale,
                    product.Name);
            }
        }

        var items = cart.Items.Select(i => new OrderItem(i.ProductId, products[i.ProductId].Name, products[i.ProductId].Price, i.Quantity));
        var order = new Order(customerId, clock.GetUtcNow(), items, shippingAddress, language);

        db.Orders.Add(order);
        db.Carts.Remove(cart);

        await db.SaveChangesAsync(cancellationToken);
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
}
