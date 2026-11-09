namespace KeycapStore.Application.Notifications;

public interface IEmailOutboxDispatcher
{
    Task<int> SendPendingAsync(CancellationToken cancellationToken = default);
}
