using KeycapStore.Application.Catalog;

namespace KeycapStore.Web.Models;

public static class RelatedKits
{
    public static IReadOnlyList<ProductSummary> Pick(ProductSummary current, IReadOnlyList<ProductSummary> catalog, int count) =>
        [.. catalog
            .Where(p => p.Id != current.Id)
            .OrderByDescending(p => SameColorway(p, current))
            .Take(count)];

    private static bool SameColorway(ProductSummary first, ProductSummary second) =>
        first.Palette is { } a
        && second.Palette is { } b
        && a.Accent == b.Accent
        && ((a.Keys == b.Keys && a.Legends == b.Legends) || (a.Keys == b.Legends && a.Legends == b.Keys));
}
