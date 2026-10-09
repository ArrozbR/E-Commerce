using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Orders;

public interface ICheckoutService
{
    Task<PlaceOrderResult> PlaceOrderAsync(string customerId, ShippingAddress shippingAddress, CancellationToken cancellationToken = default);

    Task<StartPaymentResult> StartPaymentAsync(Guid orderId, string customerId, Uri successUrl, Uri cancelUrl, CancellationToken cancellationToken = default);
}
