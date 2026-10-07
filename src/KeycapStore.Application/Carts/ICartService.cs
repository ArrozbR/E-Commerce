namespace KeycapStore.Application.Carts;

public interface ICartService
{
    Task AddItemAsync(string customerId, Guid productId, int quantity, CancellationToken cancellationToken = default);

    Task ChangeQuantityAsync(string customerId, Guid productId, int quantity, CancellationToken cancellationToken = default);

    Task RemoveItemAsync(string customerId, Guid productId, CancellationToken cancellationToken = default);

    Task<CartView> GetAsync(string customerId, CancellationToken cancellationToken = default);
}
