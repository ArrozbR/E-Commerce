using KeycapStore.Domain.Orders;

using Stripe;
using Stripe.Checkout;

namespace KeycapStore.Infrastructure.Payments;

public static class StripeCheckoutSessionFactory
{
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(31);

    public const long PixExpiresAfterSeconds = 1800;

    public static SessionCreateOptions BuildOptions(Order order, Uri successUrl, Uri cancelUrl)
    {
        var lineItems = order.Items
            .Select(i => LineItem(i.ProductName, i.UnitPrice, i.Quantity))
            .ToList();

        if (order.ShippingFee > 0)
        {
            lineItems.Add(LineItem("Frete", order.ShippingFee, 1));
        }

        return new SessionCreateOptions
        {
            Mode = "payment",
            UiMode = "hosted_page",
            LineItems = lineItems,
            ClientReferenceId = order.Id.ToString(),
            Metadata = new Dictionary<string, string> { ["order_id"] = order.Id.ToString() },
            ExpiresAt = (order.CreatedAt + SessionLifetime).UtcDateTime,
            PaymentMethodOptions = new SessionPaymentMethodOptionsOptions
            {
                Pix = new SessionPaymentMethodOptionsPixOptions { ExpiresAfterSeconds = PixExpiresAfterSeconds },
            },
            SuccessUrl = successUrl.ToString(),
            CancelUrl = cancelUrl.ToString(),
        };
    }

    public static RequestOptions BuildRequestOptions(Order order) =>
        new() { IdempotencyKey = $"session-{order.Id}" };

    public static long ToCents(decimal amount) => (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    private static SessionLineItemOptions LineItem(string name, decimal unitPrice, int quantity) => new()
    {
        Quantity = quantity,
        PriceData = new SessionLineItemPriceDataOptions
        {
            Currency = "brl",
            UnitAmount = ToCents(unitPrice),
            ProductData = new SessionLineItemPriceDataProductDataOptions { Name = name },
        },
    };
}
