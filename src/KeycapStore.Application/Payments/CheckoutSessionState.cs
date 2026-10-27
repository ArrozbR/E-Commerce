namespace KeycapStore.Application.Payments;

public sealed record CheckoutSessionState(
    string? Status,
    string? PaymentStatus,
    long? AmountTotal,
    string? Currency,
    string? PaymentIntentId);
