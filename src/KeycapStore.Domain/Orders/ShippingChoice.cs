namespace KeycapStore.Domain.Orders;

public sealed record ShippingChoice
{
    public const int MaxServiceNameLength = 100;

    public string ServiceName { get; }
    public decimal Fee { get; }
    public int DeliveryDays { get; }

    public ShippingChoice(string serviceName, decimal fee, int deliveryDays)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(serviceName.Length, MaxServiceNameLength, nameof(serviceName));
        ArgumentOutOfRangeException.ThrowIfNegative(fee);
        ArgumentOutOfRangeException.ThrowIfNegative(deliveryDays);

        ServiceName = serviceName;
        Fee = fee;
        DeliveryDays = deliveryDays;
    }
}
