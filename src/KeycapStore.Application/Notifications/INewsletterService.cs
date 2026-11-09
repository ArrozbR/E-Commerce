namespace KeycapStore.Application.Notifications;

public interface INewsletterService
{
    Task SubscribeAsync(string customerId, string language, CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(string customerId, CancellationToken cancellationToken = default);

    Task<bool> IsSubscribedAsync(string customerId, CancellationToken cancellationToken = default);
}

public interface IProductAnnouncer
{
    Task<int> AnnounceAsync(CancellationToken cancellationToken = default);
}
