using KeycapStore.Domain.Orders;

namespace KeycapStore.IntegrationTests.Fixtures;

public static class TestShipping
{
    public const int PacServiceId = 1;

    public static ShippingChoice Pac { get; } = new("Correios PAC", 15.00m, 7);
}
