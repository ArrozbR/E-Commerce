using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;
using KeycapStore.Web.Models;

namespace KeycapStore.UnitTests.Web;

public class HomeViewModelTests
{
    private static readonly KitPalette Aurora = new("#1e1b4b", "#a5f3fc", "#22c55e");

    private static ProductSummary Kit(string name, KitKind kind, int stock = 5, KitPalette? palette = null) =>
        new(Guid.NewGuid(), name, 100m, stock, Palette: palette ?? Aurora, KitKind: kind);

    [Fact]
    public void Showcase_HasOnlyTheBaseKits_AndStartsWithTheFirstPalette()
    {
        var mare = new KitPalette("#0e7490", "#ecfeff", "#fbbf24");
        var catalog = new[] { Kit("A novelties", KitKind.Novelties), Kit("B base", KitKind.Base, palette: mare), Kit("C base", KitKind.Base) };

        var home = HomeViewModel.From(catalog);

        Assert.Equal(["B base", "C base"], home.Showcase.Select(k => k.Name));
        Assert.Equal(mare, home.FirstPalette);
    }

    [Fact]
    public void Featured_PutsTheKitsThatCanBeBoughtFirst_UpToTheLimit()
    {
        var catalog = new[] { Kit("A esgotado", KitKind.Base, stock: 0), Kit("B", KitKind.Base), Kit("C", KitKind.Modifiers), Kit("D", KitKind.Novelties) };

        var home = HomeViewModel.From(catalog);

        Assert.Equal(["B", "C", "D"], home.Featured.Select(k => k.Name));
    }

    [Fact]
    public void EmptyCatalog_ShowsTheNeutralKeyboard()
    {
        var home = HomeViewModel.From([]);

        Assert.Empty(home.Showcase);
        Assert.Empty(home.Featured);
        Assert.Equal(KitPalette.Neutral, home.FirstPalette);
    }
}
