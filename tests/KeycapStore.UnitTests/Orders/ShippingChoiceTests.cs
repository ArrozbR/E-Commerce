using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class ShippingChoiceTests
{
    [Fact]
    public void ValidChoice_KeepsTheValues()
    {
        var choice = new ShippingChoice("Correios SEDEX", 59.53m, 3);

        Assert.Equal("Correios SEDEX", choice.ServiceName);
        Assert.Equal(59.53m, choice.Fee);
        Assert.Equal(3, choice.DeliveryDays);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyServiceName_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ShippingChoice(name, 10m, 3));
    }

    [Fact]
    public void ServiceNameTooLong_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingChoice(new string('a', ShippingChoice.MaxServiceNameLength + 1), 10m, 3));
    }

    [Fact]
    public void NegativeFee_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingChoice("Correios PAC", -0.01m, 7));
    }

    [Fact]
    public void NegativeDeliveryDays_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ShippingChoice("Correios PAC", 10m, -1));
    }
}
