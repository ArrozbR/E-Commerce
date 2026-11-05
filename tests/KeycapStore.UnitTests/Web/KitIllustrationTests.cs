using KeycapStore.Domain.Catalog;
using KeycapStore.Web.Models;

namespace KeycapStore.UnitTests.Web;

public class KitIllustrationTests
{
    private static readonly KitPalette Aurora = new("#1e1b4b", "#a5f3fc", "#22c55e");

    private static KitIllustration For(KitKind kind, string accent = "#22c55e") =>
        new(new KitPalette("#1e1b4b", "#a5f3fc", accent), kind);

    [Theory]
    [InlineData(KitKind.Base)]
    [InlineData(KitKind.Novelties)]
    [InlineData(KitKind.Modifiers)]
    [InlineData(KitKind.FunctionKeys)]
    public void EveryRow_EndsAtTheSameRightEdge_WithoutOverlappingKeys(KitKind kind)
    {
        var illustration = For(kind);
        var right = illustration.Left + illustration.RowWidth;

        foreach (var row in illustration.Keys.GroupBy(k => k.Y))
        {
            var keys = row.OrderBy(k => k.X).ToList();
            Assert.Equal(illustration.Left, keys[0].X, precision: 6);
            Assert.Equal(right, keys[^1].X + keys[^1].Width, precision: 6);
            for (var i = 1; i < keys.Count; i++)
            {
                Assert.True(keys[i].X > keys[i - 1].X + keys[i - 1].Width, "teclas sobrepostas");
            }
        }

        Assert.All(illustration.Keys, k => Assert.True(k.Y + k.Height <= KitIllustration.ViewHeight));
    }

    [Fact]
    public void EachKind_ShowsItsOwnKeys()
    {
        Assert.Contains(For(KitKind.Base).Keys, k => k.Legend == "Q");
        Assert.All(For(KitKind.Novelties).Keys, k => Assert.NotEqual(KeyIcon.None, k.Icon));
        Assert.Contains(For(KitKind.Modifiers).Keys, k => k.Legend == "Backspace");
        Assert.DoesNotContain(For(KitKind.Modifiers).Keys, k => k.Legend == "Q");
        Assert.Contains(For(KitKind.FunctionKeys).Keys, k => k.Legend == "F12");
    }

    [Fact]
    public void KeyName_MatchesThePhysicalKey_AndIsEmptyForDecorativeIcons()
    {
        var keyboard = For(KitKind.Base).Keys;
        var novelties = For(KitKind.Novelties).Keys;

        Assert.Contains(keyboard, k => k.KeyName == "Q");
        Assert.Contains(keyboard, k => k.KeyName == "Space" && k.Legend == "");
        Assert.Contains(novelties, k => k.Icon == KeyIcon.Return && k.KeyName == "Enter");
        Assert.All(novelties.Where(k => k.Icon != KeyIcon.Return), k => Assert.Null(k.KeyName));
    }

    [Fact]
    public void Modifiers_UseTheInvertedColors_AndAccentsTheAccentColor()
    {
        var illustration = new KitIllustration(Aurora, KitKind.Base);

        Assert.Equal(Aurora.Keys, illustration.CapColor(KeyRole.Alpha));
        Assert.Equal(Aurora.Legends, illustration.LegendColor(KeyRole.Alpha));
        Assert.Equal(Aurora.Legends, illustration.CapColor(KeyRole.Modifier));
        Assert.Equal(Aurora.Keys, illustration.LegendColor(KeyRole.Modifier));
        Assert.Equal(Aurora.Accent, illustration.CapColor(KeyRole.Accent));
    }

    [Theory]
    [InlineData("#22c55e", "#0a0a0a")]
    [InlineData("#fbbf24", "#0a0a0a")]
    [InlineData("#1e1b4b", "#ffffff")]
    [InlineData("#7c2d12", "#ffffff")]
    public void AccentLegend_IsBlackOrWhite_WhicheverReadsBetterOnTheAccent(string accent, string legend)
    {
        Assert.Equal(legend, For(KitKind.Base, accent).AccentLegend);
    }
}
