using KeycapStore.Application.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Catalog;

public sealed class CatalogQueries(AppDbContext db, TimeProvider clock) : ICatalogQueries
{
    public async Task<IReadOnlyList<ProductSummary>> ListProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);

        var preOrderIds = products.Where(p => p.PreOrder is not null).Select(p => p.Id).ToList();
        var sold = await db.SoldUnitsAsync(preOrderIds, cancellationToken);
        var now = clock.GetUtcNow();

        return [.. products.Select(p => new ProductSummary(
            p.Id,
            p.Name,
            p.Price,
            p.Stock,
            p.PreOrder is { } campaign
                ? new PreOrderSummary(campaign.EndsAt, campaign.ShipsAt, campaign.MinimumUnits, sold.GetValueOrDefault(p.Id), campaign.IsOpenAt(now))
                : null,
            p.Palette))];
    }
}
