namespace KeycapStore.Domain.Orders;

public enum OrderStatus
{
    AwaitingPayment,
    AwaitingConfirmation,
    Paid,
    Expired,
    Failed,
    PaidOutOfStock,
    Refunded,
    Picking,
    Shipped,
    Delivered,
    Cancelled,
    Withdrawn,
    AwaitingReturn,
    Returned,
}
