using KeycapStore.Domain.Catalog;
using KeycapStore.Web.Models;

namespace KeycapStore.UnitTests.Web;

public class KitIllustrationTests
{
    private static KitIllustration For(string name, string accent = "#22c55e") =>
        new(new KitPalette("#1e1b4b", "#a5f3fc", accent), name);

    [Theory]
    [InlineData("Kit Aurora (base)", "A")]
    [InlineData("Kit Maré (modificadores)", "M")]
    [InlineData("kit zéfiro", "Z")]
    [InlineData("Ébano", "É")]
    [InlineData("Kit (sem nome)", "S")]
    [InlineData("", "K")]
    public void Initial_IsTheFirstLetterAfterKit(string name, string initial)
    {
        Assert.Equal(initial, For(name).Initial);
    }

    [Theory]
    [InlineData("#22c55e", "#0a0a0a")]
    [InlineData("#fbbf24", "#0a0a0a")]
    [InlineData("#1e1b4b", "#ffffff")]
    [InlineData("#7c2d12", "#ffffff")]
    public void AccentLegend_IsBlackOrWhite_WhicheverReadsBetterOnTheAccent(string accent, string legend)
    {
        Assert.Equal(legend, For("Kit Teste", accent).AccentLegend);
    }
}
