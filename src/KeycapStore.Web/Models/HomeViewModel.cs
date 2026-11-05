using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;

namespace KeycapStore.Web.Models;

public sealed record HomeViewModel(IReadOnlyList<ProductSummary> Showcase, IReadOnlyList<ProductSummary> Featured)
{
    public const string Words = "qwerty|thock|wasd|clack|switch";

    public static HomeViewModel From(IReadOnlyList<ProductSummary> catalog, Random random, int featured = 3) => new(
        [.. catalog.Where(p => p.KitKind == KitKind.Base && p.Palette is not null)],
        [.. Shuffled(catalog.Where(p => p.CanBeBought), random).Concat(Shuffled(catalog.Where(p => !p.CanBeBought), random)).Take(featured)]);

    private static ProductSummary[] Shuffled(IEnumerable<ProductSummary> kits, Random random)
    {
        var array = kits.ToArray();
        random.Shuffle(array);
        return array;
    }

    public KitPalette FirstPalette => Showcase.FirstOrDefault()?.Palette ?? KitPalette.Neutral;
}
