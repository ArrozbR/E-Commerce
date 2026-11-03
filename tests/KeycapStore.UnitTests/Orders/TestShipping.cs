using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public static class TestShipping
{
    public static ShippingChoice Pac { get; } = new("Correios PAC", 15.00m, 7);
}
