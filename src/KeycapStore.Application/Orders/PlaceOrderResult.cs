namespace KeycapStore.Application.Orders;

public enum PlaceOrderFailure
{
    EmptyCart,
    OutOfStock,
    NoLongerForSale,
    ShippingUnavailable,
    NoShippingOption,
    ShippingOptionGone,
    PreOrderClosed,
    PreOrderMustBeAlone,
}

public sealed record PlaceOrderResult(Guid? OrderId, PlaceOrderFailure? Failure, string? ProductName)
{
    public bool Succeeded => OrderId is not null;

    public static PlaceOrderResult Success(Guid orderId) => new(orderId, null, null);

    public static PlaceOrderResult Fail(PlaceOrderFailure failure, string? productName = null) => new(null, failure, productName);
}

public sealed record ShippingOptionsResult(PlaceOrderFailure? Failure, IReadOnlyList<ShippingOption> Options)
{
    public bool Succeeded => Failure is null;

    public static ShippingOptionsResult Success(IReadOnlyList<ShippingOption> options) => new(null, options);

    public static ShippingOptionsResult Fail(PlaceOrderFailure failure) => new(failure, []);
}
