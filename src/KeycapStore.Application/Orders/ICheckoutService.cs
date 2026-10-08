using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Orders;

public interface ICheckoutService
{
    Task<PlaceOrderResult> PlaceOrderAsync(string customerId, ShippingAddress shippingAddress, CancellationToken cancellationToken = default);
}
