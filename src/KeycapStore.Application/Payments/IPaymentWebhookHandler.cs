namespace KeycapStore.Application.Payments;

public interface IPaymentWebhookHandler
{
    Task<WebhookOutcome> HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default);
}

public enum WebhookOutcome
{
    Accepted,
    Duplicate,
    InvalidSignature,
}
