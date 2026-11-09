namespace KeycapStore.Application.Notifications;

public interface INewsletterService
{
    Task SubscribeAsync(string customerId, string language, CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(string customerId, CancellationToken cancellationToken = default);

    Task<bool> IsSubscribedAsync(string customerId, CancellationToken cancellationToken = default);

    string UnsubscribeTokenFor(string customerId);

    Task<string?> EmailForUnsubscribeTokenAsync(string token, CancellationToken cancellationToken = default);

    Task<bool> UnsubscribeWithTokenAsync(string token, CancellationToken cancellationToken = default);
}

public interface IProductAnnouncer
{
    Task<int> AnnounceAsync(CancellationToken cancellationToken = default);
}
