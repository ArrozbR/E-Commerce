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

        var home = HomeViewModel.From(catalog, new Random(1));

        Assert.Equal(["B base", "C base"], home.Showcase.Select(k => k.Name));
        Assert.Equal(mare, home.FirstPalette);
    }

    [Fact]
    public void Featured_IsADrawOfThreeKitsThatCanBeBought_DifferentFromVisitToVisit()
    {
        var catalog = new[] { Kit("A esgotado", KitKind.Base, stock: 0) }
            .Concat(Enumerable.Range(1, 8).Select(i => Kit($"Kit {i}", KitKind.Base)))
            .ToArray();

        var draws = Enumerable.Range(0, 20).Select(seed => HomeViewModel.From(catalog, new Random(seed)).Featured).ToList();

        Assert.All(draws, featured =>
        {
            Assert.Equal(3, featured.Count);
            Assert.Equal(3, featured.Distinct().Count());
            Assert.All(featured, kit => Assert.True(kit.CanBeBought));
        });
        Assert.True(draws.SelectMany(f => f).Distinct().Count() > 3, "o sorteio sempre devolveu os mesmos kits");
    }

    [Fact]
    public void Featured_FillsWithKitsThatCannotBeBought_WhenThereAreNotEnough()
    {
        var catalog = new[] { Kit("A esgotado", KitKind.Base, stock: 0), Kit("B", KitKind.Base), Kit("C esgotado", KitKind.Modifiers, stock: 0) };

        var featured = HomeViewModel.From(catalog, new Random(3)).Featured;

        Assert.Equal("B", featured[0].Name);
        Assert.Equal(3, featured.Count);
    }

    [Fact]
    public void EmptyCatalog_ShowsTheNeutralKeyboard()
    {
        var home = HomeViewModel.From([], new Random(1));

        Assert.Empty(home.Showcase);
        Assert.Empty(home.Featured);
        Assert.Equal(KitPalette.Neutral, home.FirstPalette);
    }
}
