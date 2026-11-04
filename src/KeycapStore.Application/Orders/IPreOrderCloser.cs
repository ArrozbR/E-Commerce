namespace KeycapStore.Application.Orders;

public interface IPreOrderCloser
{
    Task<int> CloseDueCampaignsAsync(CancellationToken cancellationToken = default);
}
