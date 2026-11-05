using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Catalog;

internal sealed class ProductAdminService(AppDbContext db, TimeProvider clock) : IProductAdminService
{
    public async Task<IReadOnlyList<AdminProductView>> ListAsync(CancellationToken cancellationToken = default)
    {
        var products = await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        return [.. products.Select(p => new AdminProductView(p.Id, p.Name, p.Price, p.Stock, p.IsActive, p.WeightKg, p.WidthCm, p.HeightCm, p.LengthCm, p.PreOrder, p.PreOrderOutcome, p.Palette))];
    }

    public async Task<Guid> CreateAsync(string name, decimal price, int stock, ProductPackage package, CancellationToken cancellationToken = default)
    {
        var product = new Product(name, price, stock, package);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return product.Id;
    }

    public async Task<Guid> CreatePreOrderAsync(string name, decimal price, int slots, ProductPackage package, PreOrder preOrder, CancellationToken cancellationToken = default)
    {
        var product = Product.ForPreOrder(name, price, slots, package, preOrder, clock.GetUtcNow());

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return product.Id;
    }

    public async Task ReschedulePreOrderAsync(Guid productId, DateTimeOffset endsAt, DateTimeOffset shipsAt, CancellationToken cancellationToken = default)
    {
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new ProductNotFoundException(productId);

        product.ReschedulePreOrder(endsAt, shipsAt, clock.GetUtcNow());

        var saved = await db.Products
            .Where(p => p.Id == productId && p.PreOrderOutcome == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.PreOrderEndsAt, product.PreOrderEndsAt)
                .SetProperty(p => p.PreOrderShipsAt, product.PreOrderShipsAt), cancellationToken);

        if (saved == 0)
        {
            throw new InvalidOperationException("A pré-venda já foi fechada.");
        }
    }

    public Task UpdateAsync(Guid productId, string name, decimal price, ProductPackage package, CancellationToken cancellationToken = default) =>
        ApplyAsync(productId, product =>
        {
            product.Update(name, price);
            product.ChangePackage(package);
        }, cancellationToken);

    public Task ChangePaletteAsync(Guid productId, KitPalette palette, CancellationToken cancellationToken = default) =>
        ApplyAsync(productId, product => product.ChangePalette(palette), cancellationToken);

    public Task ActivateAsync(Guid productId, CancellationToken cancellationToken = default) =>
        ApplyAsync(productId, product => product.Activate(), cancellationToken);

    public Task DeactivateAsync(Guid productId, CancellationToken cancellationToken = default) =>
        ApplyAsync(productId, product => product.Deactivate(), cancellationToken);

    public async Task<StockAdjustmentResult> AdjustStockAsync(Guid productId, int delta, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(delta);

        var adjusted = await db.Products
            .Where(p => p.Id == productId && p.Stock + delta >= 0)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + delta), cancellationToken);

        if (adjusted == 1)
        {
            return StockAdjustmentResult.Adjusted;
        }

        if (!await db.Products.AnyAsync(p => p.Id == productId, cancellationToken))
        {
            throw new ProductNotFoundException(productId);
        }

        return StockAdjustmentResult.WouldGoNegative;
    }

    private async Task ApplyAsync(Guid productId, Action<Product> change, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == productId, cancellationToken)
            ?? throw new ProductNotFoundException(productId);

        change.Invoke(product);

        await db.SaveChangesAsync(cancellationToken);
    }
}
