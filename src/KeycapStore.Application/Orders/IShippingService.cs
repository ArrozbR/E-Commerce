namespace KeycapStore.Application.Orders;

public interface IShippingService
{
    Task StartPickingAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task ShipAsync(Guid orderId, string trackingCode, CancellationToken cancellationToken = default);

    Task DeliverAsync(Guid orderId, CancellationToken cancellationToken = default);
}
