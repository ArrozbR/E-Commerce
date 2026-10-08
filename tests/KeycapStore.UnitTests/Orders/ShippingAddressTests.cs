using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class ShippingAddressTests
{
    private static ShippingAddress NewAddress(string state = "SP", string postalCode = "01310-100", string? complement = "Apto 12") =>
        new("Ana Souza", "Avenida Paulista", "1000", complement, "Bela Vista", "São Paulo", state, postalCode);

    [Fact]
    public void NewAddress_KeepsAllFields()
    {
        var address = NewAddress();

        Assert.Equal("Ana Souza", address.RecipientName);
        Assert.Equal("Avenida Paulista", address.Street);
        Assert.Equal("1000", address.Number);
        Assert.Equal("Apto 12", address.Complement);
        Assert.Equal("Bela Vista", address.District);
        Assert.Equal("São Paulo", address.City);
        Assert.Equal("SP", address.State);
    }

    [Theory]
    [InlineData("01310-100")]
    [InlineData("01310100")]
    [InlineData(" 01310 100 ")]
    public void PostalCode_IsStoredWithDigitsOnly(string postalCode)
    {
        Assert.Equal("01310100", NewAddress(postalCode: postalCode).PostalCode);
    }

    [Theory]
    [InlineData("1310-100")]
    [InlineData("01310-1000")]
    [InlineData("01310-10A")]
    public void PostalCode_WithoutExactlyEightDigits_Throws(string postalCode)
    {
        Assert.Throws<ArgumentException>(() => NewAddress(postalCode: postalCode));
    }

    [Theory]
    [InlineData("sp")]
    [InlineData(" Sp ")]
    public void State_IsStoredInUppercase(string state)
    {
        Assert.Equal("SP", NewAddress(state: state).State);
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("São Paulo")]
    public void State_ThatIsNotABrazilianState_Throws(string state)
    {
        Assert.Throws<ArgumentException>(() => NewAddress(state: state));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Complement_IsOptional(string? complement)
    {
        Assert.Null(NewAddress(complement: complement).Complement);
    }

    [Fact]
    public void NewAddress_WithoutRecipientName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new ShippingAddress("  ", "Avenida Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", "01310100"));
    }
}
