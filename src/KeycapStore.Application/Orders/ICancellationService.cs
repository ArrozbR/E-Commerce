namespace KeycapStore.Application.Orders;

public enum CancelPaymentResult
{
    Requested,
    NotAwaitingPayment,
    PaymentServiceUnavailable,
    AlreadyExpired,
    AlreadyPaid,
}

public interface ICancellationService
{
    Task CancelAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);

    Task<CancelPaymentResult> CancelPaymentAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);
}
