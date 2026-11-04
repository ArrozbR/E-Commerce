using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal static class PreOrderBatches
{
    public static async Task<PreOrderOutcome?> PreOrderOutcomeAsync(this AppDbContext db, Order order, CancellationToken cancellationToken)
    {
        var outcomes = await db.PreOrderOutcomesAsync([order], cancellationToken);
        return outcomes.GetValueOrDefault(order.Id);
    }

    public static async Task<Dictionary<Guid, PreOrderOutcome?>> PreOrderOutcomesAsync(this AppDbContext db, IEnumerable<Order> orders, CancellationToken cancellationToken)
    {
        var preOrders = orders.Where(o => o.PreOrderShipsAt is not null).ToList();
        var productIds = preOrders.SelectMany(o => o.Items).Select(i => i.ProductId).Distinct().ToList();
        if (productIds.Count == 0)
        {
            return [];
        }

        var outcomes = await db.Products.AsNoTracking()
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.PreOrderOutcome, cancellationToken);

        return preOrders.ToDictionary(o => o.Id, o => outcomes.GetValueOrDefault(o.Items[0].ProductId));
    }

    public static async Task<DateTimeOffset?> CurrentShipsAtAsync(this AppDbContext db, Order order, CancellationToken cancellationToken)
    {
        if (order.PreOrderShipsAt is null)
        {
            return null;
        }

        var productId = order.Items[0].ProductId;
        return await db.Products.AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => p.PreOrderShipsAt)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
