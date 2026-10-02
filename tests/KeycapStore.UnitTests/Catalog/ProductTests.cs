using KeycapStore.Domain.Catalog;

namespace KeycapStore.UnitTests.Catalog;

public class ProductTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesProduct()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40);

        Assert.Equal("Kit Aurora (base)", product.Name);
        Assert.Equal(349.90m, product.Price);
        Assert.Equal(40, product.Stock);
        Assert.NotEqual(Guid.Empty, product.Id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Constructor_WithPriceZeroOrNegative_Throws(int price)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product("Kit", price, 10));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Constructor_WithNameWhiteSpace_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Product(name, 300, 50));
    }

    [Fact]
    public void Constructor_WithNameNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Product(null!, 100, 10));
    }

    [Theory]
    [InlineData(-1)]
    public void Constructor_WithStockNegative_Throws(int stock)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product("Kit", 100, stock));
    }


    [Fact]
    public void Constructor_WithStockZero_CreatesSoldOutProduct()
    {
        var product = new Product("Kit", 100, 0);
        Assert.Equal(0, product.Stock);
    }
}
