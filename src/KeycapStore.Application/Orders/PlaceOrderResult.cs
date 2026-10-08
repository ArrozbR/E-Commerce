namespace KeycapStore.Application.Orders;

public sealed record PlaceOrderResult(Guid? OrderId, string? Error)
{
    public bool Succeeded => OrderId is not null;

    public static PlaceOrderResult Success(Guid orderId) => new(orderId, null);

    public static PlaceOrderResult Failure(string error) => new(null, error);
}
