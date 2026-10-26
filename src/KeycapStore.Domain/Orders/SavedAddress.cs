namespace KeycapStore.Domain.Orders;

public sealed class SavedAddress
{
    public string CustomerId { get; private set; }
    public ShippingAddress Address { get; private set; }

    private SavedAddress()
    {
        CustomerId = null!;
        Address = null!;
    }

    public SavedAddress(string customerId, ShippingAddress address)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentNullException.ThrowIfNull(address);

        CustomerId = customerId;
        Address = address;
    }

    public void Replace(ShippingAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        Address = address;
    }
}
