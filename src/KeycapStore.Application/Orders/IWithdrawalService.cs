namespace KeycapStore.Application.Orders;

public interface IWithdrawalService
{
    Task WithdrawAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);
}
