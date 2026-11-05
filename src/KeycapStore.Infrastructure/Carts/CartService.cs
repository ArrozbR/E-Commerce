using KeycapStore.Application.Carts;
using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Carts;

internal sealed class CartService(AppDbContext db, TimeProvider clock) : ICartService
{
    public async Task AddItemAsync(string customerId, Guid productId, int quantity, CancellationToken cancellationToken = default)
    {
        var cart = await db.Carts.SingleOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);

        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId && p.IsActive, cancellationToken);
        if (product is null || (product.PreOrder is { } campaign && !campaign.IsOpenAt(clock.GetUtcNow())))
        {
            throw new ProductNotFoundException(productId);
        }

        var otherIds = cart?.Items.Select(i => i.ProductId).Where(id => id != productId).ToList() ?? [];
        if (otherIds.Count > 0)
        {
            var othersArePreOrder = await db.Products.AnyAsync(p => otherIds.Contains(p.Id) && p.PreOrderEndsAt != null, cancellationToken);
            var mixesPreOrder = product.PreOrder is not null || othersArePreOrder;
            if (mixesPreOrder)
            {
                throw new PreOrderMustBeAloneException(productId);
            }
        }

        var inCart = cart?.Items.FirstOrDefault(i => i.ProductId == productId)?.Quantity ?? 0;
        if ((long)inCart + quantity > product.Stock)
        {
            throw new CartStockLimitException(productId, product.Stock);
        }

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

        var stock = await db.Products.Where(p => p.Id == productId).Select(p => (int?)p.Stock).SingleOrDefaultAsync(cancellationToken);
        if (stock is { } available && quantity > available && quantity <= Cart.MaxQuantityPerItem)
        {
            throw new CartStockLimitException(productId, available);
        }

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
            .Select(i =>
            {
                var product = products[i.ProductId];
                return new CartLineView(i.ProductId, product.Name, product.Price, i.Quantity, product.IsActive, product.Stock, product.PreOrderShipsAt, product.Palette, product.KitKind);
            })
            .OrderBy(l => l.ProductName)
            .ToList();

        return new CartView(lines);
    }

    public Task<int> CountItemsAsync(string customerId, CancellationToken cancellationToken = default) =>
        db.Carts
            .Where(c => c.CustomerId == customerId)
            .SelectMany(c => c.Items)
            .SumAsync(i => i.Quantity, cancellationToken);
}
