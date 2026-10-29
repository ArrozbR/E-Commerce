using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Orders;

public interface ICheckoutService
{
    Task<PlaceOrderResult> PlaceOrderAsync(
        string customerId,
        ShippingAddress shippingAddress,
        string language = Order.DefaultLanguage,
        CancellationToken cancellationToken = default);

    Task<StartPaymentResult> StartPaymentAsync(Guid orderId, string customerId, string customerEmail, Uri returnUrl, CancellationToken cancellationToken = default);

    Task<PaymentForm?> GetPaymentFormAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);
}
