using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;

namespace KeycapStore.Web.Models;

public sealed record HomeViewModel(IReadOnlyList<ProductSummary> Showcase, IReadOnlyList<ProductSummary> Featured)
{
    public const string Words = "qwerty|thock|wasd|clack|switch";

    public static HomeViewModel From(IReadOnlyList<ProductSummary> catalog, int featured = 3) => new(
        [.. catalog.Where(p => p.KitKind == KitKind.Base && p.Palette is not null)],
        [.. catalog.OrderByDescending(p => p.CanBeBought).Take(featured)]);

    public KitPalette FirstPalette => Showcase.FirstOrDefault()?.Palette ?? KitPalette.Neutral;
}
