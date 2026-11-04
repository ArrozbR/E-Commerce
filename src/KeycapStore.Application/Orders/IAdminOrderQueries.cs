using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Orders;

public sealed record AdminOrderView(
    Guid Id,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineView> Lines,
    decimal Total,
    ShippingAddress ShippingAddress,
    string? TrackingCode,
    string? ShippingService = null,
    DateTimeOffset? PreOrderShipsAt = null,
    PreOrderOutcome? PreOrderOutcome = null)
{
    public bool WaitsForTheBatch => PreOrderShipsAt is not null && PreOrderOutcome is not Domain.Catalog.PreOrderOutcome.Confirmed;
}

public interface IAdminOrderQueries
{
    Task<IReadOnlyList<AdminOrderView>> GetShippingQueueAsync(CancellationToken cancellationToken = default);
}
