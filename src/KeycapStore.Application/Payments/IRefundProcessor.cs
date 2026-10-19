namespace KeycapStore.Application.Payments;

public interface IRefundProcessor
{
    Task<int> RefundPendingAsync(CancellationToken cancellationToken = default);
}
