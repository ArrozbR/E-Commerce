using Stripe;

namespace KeycapStore.Infrastructure.Payments;

public static class StripeRefundFactory
{
    public static RefundCreateOptions BuildOptions(string paymentIntentId) => new()
    {
        PaymentIntent = paymentIntentId,
    };

    public static RequestOptions BuildRequestOptions(Guid orderId) => new()
    {
        IdempotencyKey = $"refund-{orderId}",
    };
}
