namespace KeycapStore.Application.Orders;

public interface ICancellationService
{
    Task CancelAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);
}
