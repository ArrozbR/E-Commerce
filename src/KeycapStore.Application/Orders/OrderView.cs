using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Orders;

public sealed record OrderLineView(string ProductName, decimal UnitPrice, int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

public sealed record OrderView(
    Guid Id,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    IReadOnlyList<OrderLineView> Lines,
    decimal ShippingFee,
    decimal Total,
    ShippingAddress ShippingAddress,
    bool CanWithdraw,
    DateTimeOffset? WithdrawalDeadline,
    string? ShippingService = null,
    int? ShippingDeliveryDays = null,
    DateTimeOffset? PreOrderShipsAt = null,
    PreOrderOutcome? PreOrderOutcome = null,
    DateTimeOffset? PreOrderCurrentShipsAt = null,
    string? TrackingCode = null);

public sealed record OrderSummaryView(Guid Id, OrderStatus Status, DateTimeOffset CreatedAt, decimal Total);
