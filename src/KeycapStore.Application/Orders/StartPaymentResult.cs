namespace KeycapStore.Application.Orders;

public sealed record StartPaymentResult(bool Succeeded, string? Error)
{
    public static StartPaymentResult Success() => new(true, null);

    public static StartPaymentResult Failure(string error) => new(false, error);
}

public sealed record PaymentForm(Guid OrderId, string ClientSecret, string PublishableKey, string Language);
