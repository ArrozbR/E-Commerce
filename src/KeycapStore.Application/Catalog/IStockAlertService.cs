namespace KeycapStore.Application.Catalog;

public interface IStockAlertService
{
    Task<StockAlertResult> SubscribeAsync(string customerId, Guid productId, string language, CancellationToken cancellationToken = default);

    Task UnsubscribeAsync(string customerId, Guid productId, CancellationToken cancellationToken = default);

    Task<bool> IsSubscribedAsync(string customerId, Guid productId, CancellationToken cancellationToken = default);
}

public interface IStockAlertNotifier
{
    Task<int> NotifyAsync(CancellationToken cancellationToken = default);
}

public enum StockAlertResult
{
    Subscribed,
    AlreadySubscribed,
    NotAllowed,
}
