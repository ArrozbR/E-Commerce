using KeycapStore.Application.Orders;
using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class ShippingService(AppDbContext db) : IShippingService
{
    public Task StartPickingAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        ApplyAsync(orderId, order => order.StartPicking(), cancellationToken);

    public Task ShipAsync(Guid orderId, string trackingCode, CancellationToken cancellationToken = default) =>
        ApplyAsync(orderId, order => order.MarkShipped(trackingCode), cancellationToken);

    public Task DeliverAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        ApplyAsync(orderId, order => order.MarkDelivered(), cancellationToken);

    private async Task ApplyAsync(Guid orderId, Action<Order> transition, CancellationToken cancellationToken)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

        transition.Invoke(order);

        await db.SaveChangesAsync(cancellationToken);
    }
}
