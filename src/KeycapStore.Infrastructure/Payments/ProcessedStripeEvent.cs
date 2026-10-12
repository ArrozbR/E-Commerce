namespace KeycapStore.Infrastructure.Payments;

public sealed class ProcessedStripeEvent(string eventId, string type, DateTimeOffset processedAt)
{
    public string EventId { get; private set; } = eventId;

    public string Type { get; private set; } = type;

    public DateTimeOffset ProcessedAt { get; private set; } = processedAt;
}
