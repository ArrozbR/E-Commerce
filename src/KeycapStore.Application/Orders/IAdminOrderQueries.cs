using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Orders;

public sealed record AdminOrderView(
    Guid Id,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineView> Lines,
    decimal Total,
    ShippingAddress ShippingAddress,
    string? TrackingCode);

public interface IAdminOrderQueries
{
    Task<IReadOnlyList<AdminOrderView>> GetShippingQueueAsync(CancellationToken cancellationToken = default);
}
