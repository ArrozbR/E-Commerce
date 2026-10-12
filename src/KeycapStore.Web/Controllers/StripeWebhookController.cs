using KeycapStore.Application.Payments;

using Microsoft.AspNetCore.Mvc;

namespace KeycapStore.Web.Controllers;

[Route("webhooks/stripe")]
[IgnoreAntiforgeryToken]
public sealed class StripeWebhookController(IPaymentWebhookHandler webhooks) : Controller
{
    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = Request.Headers["Stripe-Signature"].ToString();

        var outcome = await webhooks.HandleAsync(payload, signature, cancellationToken);

        return outcome == WebhookOutcome.InvalidSignature ? BadRequest() : Ok();
    }
}
