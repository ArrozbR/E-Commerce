using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class AdminOrderQueries(AppDbContext db) : IAdminOrderQueries
{
    private static readonly OrderStatus[] ShippingQueue =
        [OrderStatus.Paid, OrderStatus.Picking, OrderStatus.Shipped, OrderStatus.Withdrawn, OrderStatus.AwaitingReturn];

    public async Task<IReadOnlyList<AdminOrderView>> GetShippingQueueAsync(CancellationToken cancellationToken = default)
    {
        var orders = await db.Orders.AsNoTracking()
            .Where(o => ShippingQueue.Contains(o.Status))
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders
            .Select(o => new AdminOrderView(
                o.Id,
                o.Status,
                o.CreatedAt,
                o.Items.Select(i => new OrderLineView(i.ProductName, i.UnitPrice, i.Quantity)).OrderBy(l => l.ProductName).ToList(),
                o.Total,
                o.ShippingAddress,
                o.TrackingCode))
            .ToList();
    }
}
