using KeycapStore.Application.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class OrderQueries(AppDbContext db) : IOrderQueries
{
    public async Task<OrderView?> GetForCustomerAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var lines = order.Items
            .Select(i => new OrderLineView(i.ProductName, i.UnitPrice, i.Quantity))
            .OrderBy(l => l.ProductName)
            .ToList();

        return new OrderView(order.Id, order.Status, order.CreatedAt, lines, order.ShippingFee, order.Total, order.ShippingAddress);
    }
}
