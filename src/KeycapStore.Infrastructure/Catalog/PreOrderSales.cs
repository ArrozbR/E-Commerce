using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Catalog;

internal static class PreOrderSales
{
    public static async Task<Dictionary<Guid, int>> SoldUnitsAsync(this AppDbContext db, IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0)
        {
            return [];
        }

        return await db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid)
            .SelectMany(o => o.Items)
            .Where(i => productIds.Contains(i.ProductId))
            .GroupBy(i => i.ProductId)
            .Select(g => new { ProductId = g.Key, Units = g.Sum(i => i.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Units, cancellationToken);
    }
}
