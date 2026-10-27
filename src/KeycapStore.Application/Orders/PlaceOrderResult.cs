namespace KeycapStore.Application.Orders;

public enum PlaceOrderFailure
{
    EmptyCart,
    OutOfStock,
    NoLongerForSale,
}

public sealed record PlaceOrderResult(Guid? OrderId, PlaceOrderFailure? Failure, string? ProductName)
{
    public bool Succeeded => OrderId is not null;

    public static PlaceOrderResult Success(Guid orderId) => new(orderId, null, null);

    public static PlaceOrderResult Fail(PlaceOrderFailure failure, string? productName = null) => new(null, failure, productName);
}
