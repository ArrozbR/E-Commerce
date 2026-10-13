using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal static class StockRelease
{
    public static async Task ReleaseStockAsync(this AppDbContext db, Order order, CancellationToken cancellationToken)
    {
        foreach (var item in order.Items.OrderBy(i => i.ProductId))
        {
            await db.Products
                .Where(p => p.Id == item.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock + item.Quantity), cancellationToken);
        }
    }
}
