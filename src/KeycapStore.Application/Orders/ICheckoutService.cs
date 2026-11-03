using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Orders;

public interface ICheckoutService
{
    Task<ShippingOptionsResult> QuoteShippingAsync(string customerId, string postalCode, CancellationToken cancellationToken = default);

    Task<PlaceOrderResult> PlaceOrderAsync(
        string customerId,
        ShippingAddress shippingAddress,
        int shippingServiceId,
        string language = Order.DefaultLanguage,
        CancellationToken cancellationToken = default);

    Task<StartPaymentResult> StartPaymentAsync(Guid orderId, string customerId, string customerEmail, Uri returnUrl, CancellationToken cancellationToken = default);

    Task<PaymentForm?> GetPaymentFormAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);
}
