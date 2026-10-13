using System.Security.Cryptography;
using System.Text;

namespace KeycapStore.IntegrationTests.Fixtures;

public static class StripeWebhookSigner
{
    public const string TestSecret = "whsec_test_secret_for_tests";

    public static string Sign(string payload, string secret = TestSecret, DateTimeOffset? signedAt = null)
    {
        var timestamp = (signedAt ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{payload}"));
        return $"t={timestamp},v1={Convert.ToHexStringLower(hash)}";
    }

    public static string Event(string id, string type) => $$"""
        {
          "id": "{{id}}",
          "object": "event",
          "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
          "type": "{{type}}",
          "data": { "object": { "id": "cs_test_fake", "object": "checkout.session" } }
        }
        """;

    // Um checkout.session.expired: a sessão venceu sem pagamento.
    public static string CheckoutExpired(string id, Guid orderId) => $$"""
        {
          "id": "{{id}}",
          "object": "event",
          "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
          "type": "checkout.session.expired",
          "data": {
            "object": {
              "id": "cs_test_{{orderId:N}}",
              "object": "checkout.session",
              "status": "expired",
              "payment_status": "unpaid",
              "metadata": { "order_id": "{{orderId}}" }
            }
          }
        }
        """;

    public static string CheckoutCompleted(string id, Guid orderId, long amountTotal, string currency = "brl", string paymentStatus = "paid") => $$"""
        {
          "id": "{{id}}",
          "object": "event",
          "api_version": "{{Stripe.StripeConfiguration.ApiVersion}}",
          "type": "checkout.session.completed",
          "data": {
            "object": {
              "id": "cs_test_{{orderId:N}}",
              "object": "checkout.session",
              "amount_total": {{amountTotal}},
              "currency": "{{currency}}",
              "payment_status": "{{paymentStatus}}",
              "metadata": { "order_id": "{{orderId}}" }
            }
          }
        }
        """;
}
