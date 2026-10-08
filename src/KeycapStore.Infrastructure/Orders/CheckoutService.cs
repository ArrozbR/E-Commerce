using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class CheckoutService(AppDbContext db, TimeProvider clock) : ICheckoutService
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
}
