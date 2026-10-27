using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class OrderLanguageTests
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static Order NewOrder(string? language = null) => language is null
        ? new Order("customer-1", DateTimeOffset.UtcNow, [new OrderItem(Guid.NewGuid(), "Kit", 100m, 1)], Curitiba)
        : new Order("customer-1", DateTimeOffset.UtcNow, [new OrderItem(Guid.NewGuid(), "Kit", 100m, 1)], Curitiba, language);

    [Fact]
    public void NewOrder_WithoutLanguage_IsInPortuguese()
    {
        Assert.Equal("pt-BR", NewOrder().Language);
    }

    [Fact]
    public void NewOrder_KeepsTheLanguageItWasPlacedIn()
    {
        Assert.Equal("en-US", NewOrder("en-US").Language);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("xx-muito-comprido")]
    public void NewOrder_WithAnInvalidLanguage_Throws(string language)
    {
        Assert.ThrowsAny<ArgumentException>(() => NewOrder(language));
    }
}
