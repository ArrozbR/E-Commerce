using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class OrderItemTests
{
    private static readonly Guid AnyProduct = Guid.NewGuid();

    [Fact]
    public void NewItem_KeepsTheSnapshotOfNameAndPrice()
    {
        var item = new OrderItem(AnyProduct, "Kit Aurora (base)", 349.90m, 2);

        Assert.Equal(AnyProduct, item.ProductId);
        Assert.Equal("Kit Aurora (base)", item.ProductName);
        Assert.Equal(349.90m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void LineTotal_IsPriceTimesQuantity()
    {
        var item = new OrderItem(AnyProduct, "Kit Aurora (base)", 349.90m, 3);

        Assert.Equal(1049.70m, item.LineTotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NewItem_WithQuantityZeroOrLess_Throws(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderItem(AnyProduct, "Kit", 10m, quantity));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NewItem_WithPriceZeroOrLess_Throws(decimal price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OrderItem(AnyProduct, "Kit", price, 1));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void NewItem_WithoutName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new OrderItem(AnyProduct, name, 10m, 1));
    }
}
