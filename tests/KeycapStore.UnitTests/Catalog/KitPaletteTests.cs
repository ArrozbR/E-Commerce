using KeycapStore.Domain.Catalog;

namespace KeycapStore.UnitTests.Catalog;

public class KitPaletteTests
{
    [Fact]
    public void Constructor_WithHexColors_KeepsThemInLowerCase()
    {
        var palette = new KitPalette("#1E293B", "#a7f3d0", "#8B5cF6");

        Assert.Equal("#1e293b", palette.Keys);
        Assert.Equal("#a7f3d0", palette.Legends);
        Assert.Equal("#8b5cf6", palette.Accent);
    }

    [Theory]
    [InlineData("1e293b")]
    [InlineData("#1e293")]
    [InlineData("#1e293bb")]
    [InlineData("#1e29zz")]
    [InlineData("red")]
    [InlineData("")]
    [InlineData("#1e293b\" onload=\"x")]
    public void Constructor_WithAnythingButAHexColor_Throws(string color)
    {
        Assert.Throws<ArgumentException>(() => new KitPalette(color, "#ffffff", "#000000"));
        Assert.Throws<ArgumentException>(() => new KitPalette("#ffffff", color, "#000000"));
        Assert.Throws<ArgumentException>(() => new KitPalette("#ffffff", "#000000", color));
    }

    [Fact]
    public void IsHexColor_WithNull_IsFalse()
    {
        Assert.False(KitPalette.IsHexColor(null));
    }

    [Fact]
    public void NewProduct_HasTheNeutralPalette()
    {
        var product = new Product("Kit Neutro", 100m, 1, TestPackage.Standard);

        Assert.Equal(KitPalette.Neutral, product.Palette);
    }

    [Fact]
    public void NewProduct_IsABaseKit_AndTheKindCanChange()
    {
        var product = new Product("Kit Tipo", 100m, 1, TestPackage.Standard);
        Assert.Equal(KitKind.Base, product.KitKind);

        product.ChangeKitKind(KitKind.FunctionKeys);

        Assert.Equal(KitKind.FunctionKeys, product.KitKind);
    }

    [Fact]
    public void ChangeKitKind_WithAnUnknownValue_Throws()
    {
        var product = new Product("Kit Tipo Inválido", 100m, 1, TestPackage.Standard);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.ChangeKitKind((KitKind)99));
        Assert.Equal(KitKind.Base, product.KitKind);
    }

    [Fact]
    public void ChangePalette_ReplacesTheThreeColors()
    {
        var product = new Product("Kit Novas Cores", 100m, 1, TestPackage.Standard);
        var palette = new KitPalette("#e0f2fe", "#0c4a6e", "#0ea5e9");

        product.ChangePalette(palette);

        Assert.Equal(palette, product.Palette);
    }
}
