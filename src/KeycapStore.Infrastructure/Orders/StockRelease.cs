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

    public static async Task<bool> TryReserveStockAsync(this AppDbContext db, Order order, CancellationToken cancellationToken)
    {
        var transaction = db.Database.CurrentTransaction
            ?? throw new InvalidOperationException("A reserva de estoque precisa de uma transação aberta.");

        await transaction.CreateSavepointAsync("reserve", cancellationToken);

        foreach (var item in order.Items.OrderBy(i => i.ProductId))
        {
            var reserved = await db.Products
                .Where(p => p.Id == item.ProductId && p.Stock >= item.Quantity && p.IsActive)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - item.Quantity), cancellationToken);

            if (reserved == 0)
            {
                await transaction.RollbackToSavepointAsync("reserve", cancellationToken);
                return false;
            }
        }

        return true;
    }
}
