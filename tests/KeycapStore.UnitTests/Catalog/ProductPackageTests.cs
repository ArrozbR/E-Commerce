using System.Globalization;

using KeycapStore.Domain.Catalog;

namespace KeycapStore.UnitTests.Catalog;

public class ProductPackageTests
{
    [Fact]
    public void ValidPackage_KeepsTheValues()
    {
        var package = new ProductPackage(0.8m, 30, 6, 20);

        Assert.Equal(0.8m, package.WeightKg);
        Assert.Equal(30, package.WidthCm);
        Assert.Equal(6, package.HeightCm);
        Assert.Equal(20, package.LengthCm);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-0.5")]
    [InlineData("30.001")]
    [InlineData("0.0005")]
    public void InvalidWeight_Throws(string weight)
    {
        var weightKg = decimal.Parse(weight, CultureInfo.InvariantCulture);

        Assert.Throws<ArgumentOutOfRangeException>(() => new ProductPackage(weightKg, 30, 6, 20));
    }

    [Theory]
    [InlineData("0.001")]
    [InlineData("30")]
    public void WeightAtTheLimits_IsAccepted(string weight)
    {
        var weightKg = decimal.Parse(weight, CultureInfo.InvariantCulture);

        Assert.Equal(weightKg, new ProductPackage(weightKg, 30, 6, 20).WeightKg);
    }

    [Theory]
    [InlineData(0, 6, 20, "widthCm")]
    [InlineData(-1, 6, 20, "widthCm")]
    [InlineData(101, 6, 20, "widthCm")]
    [InlineData(30, 0, 20, "heightCm")]
    [InlineData(30, 101, 20, "heightCm")]
    [InlineData(30, 6, 0, "lengthCm")]
    [InlineData(30, 6, 101, "lengthCm")]
    public void InvalidSide_Throws_NamingTheSide(int widthCm, int heightCm, int lengthCm, string side)
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new ProductPackage(0.8m, widthCm, heightCm, lengthCm));

        Assert.Equal(side, error.ParamName);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void SidesAtTheLimits_AreAccepted(int sideCm)
    {
        var package = new ProductPackage(0.8m, sideCm, sideCm, sideCm);

        Assert.Equal(sideCm, package.WidthCm);
        Assert.Equal(sideCm, package.HeightCm);
        Assert.Equal(sideCm, package.LengthCm);
    }
}
