namespace KeycapStore.Application.Orders;

public enum CancelPaymentResult
{
    Requested,
    NotAwaitingPayment,
    PaymentServiceUnavailable,
}

public interface ICancellationService
{
    Task CancelAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);

    Task<CancelPaymentResult> CancelPaymentAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);
}
