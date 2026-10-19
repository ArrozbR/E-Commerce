using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Payments;

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutSessionAsync(Order order, Uri successUrl, Uri cancelUrl, CancellationToken cancellationToken = default);

    Task RefundAsync(Guid orderId, string paymentIntentId, CancellationToken cancellationToken = default);

    Task<string> GetPaymentIntentIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default);
}

public sealed record CheckoutSession(string Id, Uri Url);

public sealed class PaymentGatewayException(string message, Exception? innerException = null)
    : Exception(message, innerException);
