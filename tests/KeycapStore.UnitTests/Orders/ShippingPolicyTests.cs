using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class ShippingPolicyTests
{
    [Theory]
    [InlineData("DF")]
    [InlineData("GO")]
    [InlineData("MT")]
    [InlineData("MS")]
    [InlineData("SP")]
    [InlineData("RJ")]
    public void FreeShippingStates_PayNothing(string state)
    {
        Assert.Equal(0m, ShippingPolicy.FeeFor(state));
    }

    [Theory]
    [InlineData("MG")]
    [InlineData("ES")]
    [InlineData("BA")]
    [InlineData("RS")]
    [InlineData("AM")]
    public void OtherStates_PayTheStandardFee(string state)
    {
        Assert.Equal(15.00m, ShippingPolicy.FeeFor(state));
    }
}
