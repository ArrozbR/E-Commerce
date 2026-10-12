using KeycapStore.Application.Payments;

using Microsoft.Extensions.Options;

using Stripe;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class StripeWebhookHandler(IOptions<StripeOptions> options) : IPaymentWebhookHandler
{
    public Task<WebhookOutcome> HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
    {
        try
        {
            EventUtility.ConstructEvent(payload, signatureHeader, options.Value.WebhookSecret);
        }
        catch (StripeException)
        {
            return Task.FromResult(WebhookOutcome.InvalidSignature);
        }

        return Task.FromResult(WebhookOutcome.Accepted);
    }
}
