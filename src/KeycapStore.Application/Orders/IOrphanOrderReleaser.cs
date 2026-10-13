namespace KeycapStore.Application.Orders;

public interface IOrphanOrderReleaser
{
    Task<int> ReleaseOrphansAsync(CancellationToken cancellationToken = default);
}
