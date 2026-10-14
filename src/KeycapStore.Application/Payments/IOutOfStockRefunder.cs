namespace KeycapStore.Application.Payments;

public interface IOutOfStockRefunder
{
    Task<int> RefundPendingAsync(CancellationToken cancellationToken = default);
}
