using KeycapStore.Domain.Catalog;

namespace KeycapStore.UnitTests.Catalog;

public class ProductTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesProduct()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product("Kit", price, 10, TestPackage.Standard));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   ")]
    public void Constructor_WithNameWhiteSpace_Throws(string name)
    {
        Assert.ThrowsAny<ArgumentException>(() => new Product(name, 300, 50, TestPackage.Standard));
    }

    [Fact]
    public void Constructor_WithNameNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Product(null!, 100, 10, TestPackage.Standard));
    }

    [Theory]
    [InlineData(-1)]
    public void Constructor_WithStockNegative_Throws(int stock)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Product("Kit", 100, stock, TestPackage.Standard));
    }


    [Fact]
    public void Constructor_WithStockZero_CreatesSoldOutProduct()
    {
        var product = new Product("Kit", 100, 0, TestPackage.Standard);
        Assert.Equal(0, product.Stock);
    }

    [Fact]
    public void Constructor_WithNameAt150Characters_CreatesProduct()
    {
        var name = new string('a', 150);

        var product = new Product(name, 100, 10, TestPackage.Standard);

        Assert.Equal(150, product.Name.Length);
    }

    [Fact]
    public void Constructor_WithNameOver150Characters_Throws()
    {
        var name = new string('a', 151);

        Assert.Throws<ArgumentOutOfRangeException>(() => new Product(name, 100, 10, TestPackage.Standard));
    }

    [Fact]
    public void NewProduct_IsActive()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

        Assert.True(product.IsActive);
    }

    [Fact]
    public void Update_ChangesNameAndPrice_AndKeepsTheStock()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

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
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.Update("Outro nome", price));

        Assert.Equal("Kit Aurora (base)", product.Name);
        Assert.Equal(349.90m, product.Price);
    }

    [Fact]
    public void Update_WithNameWhiteSpace_Throws()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

        Assert.Throws<ArgumentException>(() => product.Update("   ", 399.90m));

        Assert.Equal(349.90m, product.Price);
    }

    [Fact]
    public void Update_WithNameOver150Characters_Throws()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.Update(new string('a', Product.MaxNameLength + 1), 399.90m));
    }

    [Fact]
    public void Deactivate_ThenActivate_GoesBackOnSale()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

        product.Deactivate();
        Assert.False(product.IsActive);

        product.Activate();
        Assert.True(product.IsActive);
    }

    [Fact]
    public void Constructor_KeepsThePackage()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, new ProductPackage(0.8m, 30, 6, 20));

        Assert.Equal(0.8m, product.WeightKg);
        Assert.Equal(30, product.WidthCm);
        Assert.Equal(6, product.HeightCm);
        Assert.Equal(20, product.LengthCm);
        Assert.Equal(new ProductPackage(0.8m, 30, 6, 20), product.Package);
    }

    [Fact]
    public void Constructor_WithoutPackage_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new Product("Kit", 100, 10, null!));
    }

    [Fact]
    public void ChangePackage_ReplacesAllTheMeasures()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

        product.ChangePackage(new ProductPackage(1.25m, 40, 8, 25));

        Assert.Equal(new ProductPackage(1.25m, 40, 8, 25), product.Package);
    }

    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static PreOrder Campaign(int endsInDays = 10, int shipsInDays = 40, int minimum = 20) =>
        new(Now.AddDays(endsInDays), Now.AddDays(shipsInDays), minimum);

    [Fact]
    public void NormalProduct_IsNotAPreOrder()
    {
        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

        Assert.Null(product.PreOrder);
    }

    [Fact]
    public void ForPreOrder_UsesTheSlotsAsStock_AndKeepsTheCampaign()
    {
        var product = Product.ForPreOrder("Kit Nebulosa (base)", 399.90m, 50, TestPackage.Standard, Campaign(), Now);

        Assert.Equal(50, product.Stock);
        Assert.True(product.IsActive);
        Assert.Equal(Campaign(), product.PreOrder);
    }

    [Fact]
    public void ForPreOrder_ThatAlreadyEnded_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, new PreOrder(Now, Now.AddDays(30), 5), Now));
    }

    [Fact]
    public void ForPreOrder_ShippingAfter80Days_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, new PreOrder(Now.AddDays(10), Now.AddDays(80).AddMinutes(1), 5), Now));
    }

    [Fact]
    public void ForPreOrder_ShippingIn80Days_IsAccepted()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, new PreOrder(Now.AddDays(10), Now.AddDays(80), 5), Now);

        Assert.Equal(Now.AddDays(80), product.PreOrder!.ShipsAt);
    }

    [Fact]
    public void ForPreOrder_MinimumAboveTheSlots_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(minimum: 51), Now));
    }

    [Fact]
    public void ForPreOrder_MinimumEqualToTheSlots_IsAccepted()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(minimum: 50), Now);

        Assert.Equal(50, product.PreOrder!.MinimumUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ForPreOrder_WithoutSlots_Throws(int slots)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Product.ForPreOrder("Kit", 100m, slots, TestPackage.Standard, Campaign(minimum: 1), Now));
    }

    [Fact]
    public void ConfirmPreOrder_TakesTheProductOffSale()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);

        product.ConfirmPreOrder();

        Assert.Equal(PreOrderOutcome.Confirmed, product.PreOrderOutcome);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void CancelPreOrder_TakesTheProductOffSale()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);

        product.CancelPreOrder();

        Assert.Equal(PreOrderOutcome.Cancelled, product.PreOrderOutcome);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void ClosingTwice_Throws()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);
        product.ConfirmPreOrder();

        Assert.Throws<InvalidOperationException>(product.CancelPreOrder);
    }

    [Fact]
    public void ClosingANormalProduct_Throws()
    {
        var product = new Product("Kit", 100m, 5, TestPackage.Standard);

        Assert.Throws<InvalidOperationException>(product.ConfirmPreOrder);
    }

    [Fact]
    public void ForPreOrder_RemembersWhenTheCampaignWasCreated()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);

        Assert.Equal(Now, product.PreOrderOpenedAt);
    }

    [Fact]
    public void ReschedulePreOrder_ChangesTheDates_AndKeepsTheMinimum()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(minimum: 20), Now);

        product.ReschedulePreOrder(Now.AddDays(20), Now.AddDays(60), Now.AddDays(5));

        Assert.Equal(new PreOrder(Now.AddDays(20), Now.AddDays(60), 20), product.PreOrder);
    }

    [Fact]
    public void ReschedulePreOrder_CanReopenACampaignThatEndedButIsNotClosed()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(endsInDays: 1), Now);

        product.ReschedulePreOrder(Now.AddDays(3), Now.AddDays(40), Now.AddDays(2));

        Assert.True(product.PreOrder!.IsOpenAt(Now.AddDays(2)));
    }

    [Fact]
    public void ReschedulePreOrder_EndNotAfterNow_Throws()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.ReschedulePreOrder(Now.AddDays(5), Now.AddDays(40), Now.AddDays(5)));
    }

    [Fact]
    public void ReschedulePreOrder_ShippingAfter80DaysFromTheCreation_Throws_EvenIfWithin80DaysFromNow()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            product.ReschedulePreOrder(Now.AddDays(30), Now.AddDays(80).AddMinutes(1), Now.AddDays(20)));
    }

    [Fact]
    public void ReschedulePreOrder_ShippingIn80DaysFromTheCreation_IsAccepted()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);

        product.ReschedulePreOrder(Now.AddDays(30), Now.AddDays(80), Now.AddDays(20));

        Assert.Equal(Now.AddDays(80), product.PreOrder!.ShipsAt);
    }

    [Fact]
    public void ReschedulePreOrder_AfterTheClosing_Throws()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);
        product.ConfirmPreOrder();

        Assert.Throws<InvalidOperationException>(() => product.ReschedulePreOrder(Now.AddDays(20), Now.AddDays(60), Now));
    }

    [Fact]
    public void ReschedulePreOrder_OfANormalProduct_Throws()
    {
        var product = new Product("Kit", 100m, 5, TestPackage.Standard);

        Assert.Throws<InvalidOperationException>(() => product.ReschedulePreOrder(Now.AddDays(20), Now.AddDays(60), Now));
    }

    [Fact]
    public void Activate_ClosedPreOrder_Throws_AndStaysOffSale()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);
        product.CancelPreOrder();

        Assert.Throws<InvalidOperationException>(product.Activate);
        Assert.False(product.IsActive);
    }

    [Fact]
    public void Activate_OpenPreOrderTakenOffSale_GoesBackOnSale()
    {
        var product = Product.ForPreOrder("Kit", 100m, 50, TestPackage.Standard, Campaign(), Now);
        product.Deactivate();

        product.Activate();

        Assert.True(product.IsActive);
    }
}
