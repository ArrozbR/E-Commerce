using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class SavedAddressTests
{
    private static readonly ShippingAddress Paulista =
        new("Ana Souza", "Avenida Paulista", "1000", "Apto 12", "Bela Vista", "São Paulo", "SP", "01310100");

    private static readonly ShippingAddress Curitiba =
        new("Ana Souza", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    [Fact]
    public void NewSavedAddress_KeepsTheCustomerAndTheAddress()
    {
        var saved = new SavedAddress("customer-1", Paulista);

        Assert.Equal("customer-1", saved.CustomerId);
        Assert.Same(Paulista, saved.Address);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewSavedAddress_WithoutCustomer_Throws(string customerId)
    {
        Assert.ThrowsAny<ArgumentException>(() => new SavedAddress(customerId, Paulista));
    }

    [Fact]
    public void NewSavedAddress_WithoutAddress_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SavedAddress("customer-1", null!));
    }

    [Fact]
    public void Replace_SwapsTheAddress_AndKeepsTheCustomer()
    {
        var saved = new SavedAddress("customer-1", Paulista);

        saved.Replace(Curitiba);

        Assert.Same(Curitiba, saved.Address);
        Assert.Equal("customer-1", saved.CustomerId);
    }

    [Fact]
    public void Replace_WithNull_Throws_AndKeepsTheOldAddress()
    {
        var saved = new SavedAddress("customer-1", Paulista);

        Assert.Throws<ArgumentNullException>(() => saved.Replace(null!));

        Assert.Same(Paulista, saved.Address);
    }
}
