using KeycapStore.Application.Carts;
using KeycapStore.Domain.Carts;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Carts;

internal sealed class CartService(AppDbContext db) : ICartService
{
    public async Task AddItemAsync(string customerId, Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        var cart = await db.Carts.SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is null)
        {
            cart = new Cart(customerId);
            db.Carts.Add(cart);
        }

        cart.AddItem(productId, quantity);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangeQuantityAsync(string customerId, Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        var cart = await db.Carts.SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken)
            ?? throw new CartItemNotFoundException(productId);

        cart.ChangeQuantity(productId, quantity);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveItemAsync(string customerId, Guid productId, CancellationToken cancellationToken = default)
    {
        var cart = await db.Carts.SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is null)
        {
            return;
        }

        cart.RemoveItem(productId);

        await db.SaveChangesAsync(cancellationToken);
    }


    public async Task<CartView> GetAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var cart = await db.Carts.AsNoTracking()
            .SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return CartView.Empty;
        }

        var productIds = cart.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var lines = cart.Items
            .Select(i => new CartLineView(i.ProductId, products[i.ProductId].Name, products[i.ProductId].Price, i.Quantity))
            .OrderBy(l => l.ProductName)
            .ToList();

        return new CartView(lines);
    }
}
