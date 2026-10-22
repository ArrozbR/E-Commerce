using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Catalog;

internal sealed class ProductAdminService(AppDbContext db) : IProductAdminService
{
    public async Task<IReadOnlyList<AdminProductView>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new AdminProductView(p.Id, p.Name, p.Price, p.Stock, p.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid> CreateAsync(string name, decimal price, int stock, CancellationToken cancellationToken = default)
    {
        var product = new Product(name, price, stock);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return product.Id;
    }

    public Task UpdateAsync(Guid productId, string name, decimal price, CancellationToken cancellationToken = default) =>
        ApplyAsync(productId, product => product.Update(name, price), cancellationToken);

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
