using KeycapStore.Application.Orders;
using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class CheckoutService(AppDbContext db, TimeProvider clock, IPaymentGateway payments) : ICheckoutService
{
    public async Task<PlaceOrderResult> PlaceOrderAsync(string customerId, ShippingAddress shippingAddress, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var cart = await db.Carts.SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return PlaceOrderResult.Failure("Seu carrinho está vazio.");
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        foreach (var item in cart.Items.OrderBy(i => i.ProductId))
        {
            var reserved = await db.Products
                .Where(p => p.Id == item.ProductId && p.Stock >= item.Quantity)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity), cancellationToken);

            if (reserved == 0)
            {
                return PlaceOrderResult.Failure($"{products[item.ProductId].Name} não tem estoque suficiente.");
            }
        }

        var items = cart.Items.Select(i => new OrderItem(i.ProductId, products[i.ProductId].Name, products[i.ProductId].Price, i.Quantity));
        var order = new Order(customerId, clock.GetUtcNow(), items, shippingAddress);

        db.Orders.Add(order);
        db.Carts.Remove(cart);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return PlaceOrderResult.Success(order.Id);
    }

    public async Task<StartPaymentResult> StartPaymentAsync(Guid orderId, string customerId, Uri successUrl, Uri cancelUrl, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);
        if (order is null)
        {
            return StartPaymentResult.Failure("Pedido não encontrado.");
        }

        CheckoutSession session;
        try
        {
            session = await payments.CreateCheckoutSessionAsync(order, successUrl, cancelUrl, cancellationToken);
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

        return StartPaymentResult.Success(session.Url);
    }
}
