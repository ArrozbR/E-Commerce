namespace KeycapStore.Application.Orders;

public sealed record StartPaymentResult(Uri? PaymentUrl, string? Error)
{
    public bool Succeeded => PaymentUrl is not null;

    public static StartPaymentResult Success(Uri paymentUrl) => new(paymentUrl, null);

    public static StartPaymentResult Failure(string error) => new(null, error);
}
