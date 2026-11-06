using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;
using KeycapStore.Web.Models;

namespace KeycapStore.UnitTests.Web;

public class CatalogFilterTests
{
    private static readonly KitPalette Aurora = new("#1e1b4b", "#a5f3fc", "#22c55e");
    private static readonly KitPalette AuroraInverted = new("#a5f3fc", "#1e1b4b", "#22c55e");
    private static readonly KitPalette Mare = new("#0e7490", "#ecfeff", "#fbbf24");

    private static readonly ProductSummary[] Catalog =
    [
        new(Guid.NewGuid(), "Kit Aurora (base)", 349.90m, 40, Palette: Aurora, KitKind: KitKind.Base),
        new(Guid.NewGuid(), "Kit Aurora (novelties)", 129.90m, 3, Palette: AuroraInverted, KitKind: KitKind.Novelties),
        new(Guid.NewGuid(), "Kit Maré (base)", 319.90m, 25, Palette: Mare, KitKind: KitKind.Base),
        new(Guid.NewGuid(), "Kit Maré (modificadores)", 149.90m, 0, Palette: new KitPalette("#ecfeff", "#0e7490", "#fbbf24"), KitKind: KitKind.Modifiers),
    ];

    private static IEnumerable<string> Names(CatalogFilter filter) => filter.Apply(Catalog).Select(k => k.Name);

    [Theory]
    [InlineData("mare")]
    [InlineData("MARÉ")]
    [InlineData("  maré ")]
    public void Search_IgnoresAccentsCaseAndSpaces(string q)
    {
        Assert.Equal(["Kit Maré (base)", "Kit Maré (modificadores)"], Names(new CatalogFilter { Q = q }));
    }

    [Fact]
    public void Kind_KeepsOnlyThatKind_AndAnUnknownKindIsIgnored()
    {
        Assert.Equal(["Kit Aurora (novelties)"], Names(new CatalogFilter { Kind = KitKind.Novelties }));
        Assert.Equal(4, Names(new CatalogFilter { Kind = (KitKind)99 }).Count());
    }

    [Fact]
    public void InStock_HidesWhatCannotBeBought()
    {
        Assert.DoesNotContain("Kit Maré (modificadores)", Names(new CatalogFilter { InStock = true }));
    }

    [Theory]
    [InlineData(CatalogFilter.LowestPrice, "Kit Aurora (novelties)")]
    [InlineData(CatalogFilter.HighestPrice, "Kit Aurora (base)")]
    [InlineData("anything-else", "Kit Aurora (base)")]
    public void Sort_ChangesTheOrder_AndAnUnknownSortKeepsTheCatalogOrder(string sort, string first)
    {
        Assert.Equal(first, Names(new CatalogFilter { Sort = sort }).First());
    }

    [Fact]
    public void Colorways_GroupKitsWithTheSameThreeColors_InAnyRole()
    {
        var colorways = CatalogFilter.ColorwaysOf(Catalog);

        Assert.Equal(["Kit Aurora", "Kit Maré"], colorways.Select(c => c.Label));
        Assert.Equal(["Kit Aurora (base)", "Kit Aurora (novelties)"], Names(new CatalogFilter { Colorway = colorways[0].Key }));
    }

    [Fact]
    public void Filters_Combine()
    {
        var filter = new CatalogFilter { Q = "kit", Kind = KitKind.Base, InStock = true, Sort = CatalogFilter.LowestPrice };

        Assert.Equal(["Kit Maré (base)", "Kit Aurora (base)"], Names(filter));
    }
}
