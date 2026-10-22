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

    [Fact]
    public void Constructor_WithNameAt150Characters_CreatesProduct()
    {
        var name = new string('a', 150);

        var product = new Product(name, 100, 10);

        Assert.Equal(150, product.Name.Length);
    }

    [Fact]
    public void Constructor_WithNameOver150Characters_Throws()
    {
        var name = new string('a', 151);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(name, 100, 10));
    }

    [Fact]
    public void NewProduct_IsActive()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40);

        Assert.True(product.IsActive);
    }

    [Fact]
    public void Update_ChangesNameAndPrice_AndKeepsTheStock()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40);

        product.Update("Kit Aurora (base) v2", 399.90m);

        Assert.Equal("Kit Aurora (base) v2", product.Name);
        Assert.Equal(399.90m, product.Price);
        Assert.Equal(40, product.Stock);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_WithPriceZeroOrNegative_Throws_AndKeepsTheOldValues(int price)
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.Update("Outro nome", price));

        Assert.Equal("Kit Aurora (base)", product.Name);
        Assert.Equal(349.90m, product.Price);
    }

    [Fact]
    public void Update_WithNameWhiteSpace_Throws()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40);

        Assert.Throws<ArgumentException>(() => product.Update("   ", 399.90m));

        Assert.Equal(349.90m, product.Price);
    }

    [Fact]
    public void Update_WithNameOver150Characters_Throws()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.Update(new string('a', Product.MaxNameLength + 1), 399.90m));
    }

    [Fact]
    public void Deactivate_ThenActivate_GoesBackOnSale()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40);

        product.Deactivate();
        Assert.False(product.IsActive);

        product.Activate();
        Assert.True(product.IsActive);
    }
}
