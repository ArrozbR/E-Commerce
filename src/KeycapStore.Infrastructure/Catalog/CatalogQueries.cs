using KeycapStore.Application.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Catalog;

public sealed class CatalogQueries(AppDbContext db) : ICatalogQueries
{
    public async Task<IReadOnlyList<ProductSummary>> ListProductsAsync(CancellationToken cancellationToken = default)
    {
        return await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => new ProductSummary(p.Id, p.Name, p.Price, p.Stock))
            .ToListAsync(cancellationToken);
    }
}
