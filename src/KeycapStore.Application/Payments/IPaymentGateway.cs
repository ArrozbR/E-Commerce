using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Payments;

public interface IPaymentGateway
{
    Task<CheckoutSession> CreateCheckoutSessionAsync(Order order, string customerEmail, Uri returnUrl, CancellationToken cancellationToken = default);

    Task RefundAsync(Guid orderId, string paymentIntentId, CancellationToken cancellationToken = default);

    Task<string> GetPaymentIntentIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default);

    Task<CheckoutSessionState> GetCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default);
}

public sealed record CheckoutSession(string Id, string ClientSecret);

public sealed class PaymentGatewayException(string message, Exception? innerException = null)
    : Exception(message, innerException);
