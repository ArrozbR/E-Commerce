using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Orders;

public interface ISavedAddressService
{
    Task<ShippingAddress?> GetAsync(string customerId, CancellationToken cancellationToken = default);

    Task SaveAsync(string customerId, ShippingAddress address, CancellationToken cancellationToken = default);

    Task RemoveAsync(string customerId, CancellationToken cancellationToken = default);
}
