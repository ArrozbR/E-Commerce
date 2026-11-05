using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;
using KeycapStore.Web.Models;

namespace KeycapStore.UnitTests.Web;

public class RelatedKitsTests
{
    private static ProductSummary Kit(string name, KitPalette palette) =>
        new(Guid.NewGuid(), name, 100m, 5, Palette: palette);

    [Fact]
    public void Pick_PutsTheSameColorwayFirst_AndLeavesTheKitItselfOut()
    {
        var auroraBase = Kit("Kit Aurora (base)", new KitPalette("#1e1b4b", "#a5f3fc", "#22c55e"));
        var auroraNovelties = Kit("Kit Aurora (novelties)", new KitPalette("#a5f3fc", "#1e1b4b", "#22c55e"));
        var mareBase = Kit("Kit Maré (base)", new KitPalette("#0e7490", "#ecfeff", "#fbbf24"));
        var otherAccent = Kit("Kit Outro Destaque", new KitPalette("#1e1b4b", "#a5f3fc", "#ef4444"));
        var catalog = new[] { auroraBase, mareBase, otherAccent, auroraNovelties };

        var related = RelatedKits.Pick(auroraBase, catalog, 3);

        Assert.Equal(new[] { auroraNovelties, mareBase, otherAccent }, related);
    }

    [Fact]
    public void Pick_ReturnsAtMostTheRequestedCount()
    {
        var palette = KitPalette.Neutral;
        var catalog = Enumerable.Range(1, 6).Select(i => Kit($"Kit {i}", palette)).ToList();

        Assert.Equal(3, RelatedKits.Pick(catalog[0], catalog, 3).Count);
    }
}
