using System.Globalization;
using System.Text;

using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;

namespace KeycapStore.Web.Models;

public sealed record Colorway(string Key, KitPalette Palette, string Label);

public sealed record CatalogPageViewModel(CatalogFilter Filter, IReadOnlyList<ProductSummary> Products, IReadOnlyList<Colorway> Colorways);

public sealed class CatalogFilter
{
    public const string ByName = "name";
    public const string LowestPrice = "price-asc";
    public const string HighestPrice = "price-desc";
    public const int MaxSearchLength = 100;

    public string? Q { get; set; }

    public KitKind? Kind { get; set; }

    public string? Colorway { get; set; }

    public bool InStock { get; set; }

    public string? Sort { get; set; }

    public IReadOnlyList<ProductSummary> Apply(IReadOnlyList<ProductSummary> catalog)
    {
        IEnumerable<ProductSummary> kits = catalog;

        var search = Q?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            var term = Fold(search.Length > MaxSearchLength ? search[..MaxSearchLength] : search);
            kits = kits.Where(k => Fold(k.Name).Contains(term, StringComparison.Ordinal));
        }

        if (Kind is { } kind && Enum.IsDefined(kind))
        {
            kits = kits.Where(k => k.KitKind == kind);
        }

        if (!string.IsNullOrEmpty(Colorway))
        {
            kits = kits.Where(k => KeyOf(k.Palette) == Colorway);
        }

        if (InStock)
        {
            kits = kits.Where(k => k.CanBeBought);
        }

        kits = Sort switch
        {
            LowestPrice => kits.OrderBy(k => k.Price),
            HighestPrice => kits.OrderByDescending(k => k.Price),
            _ => kits,
        };

        return [.. kits];
    }

    public static IReadOnlyList<Colorway> ColorwaysOf(IReadOnlyList<ProductSummary> catalog) =>
        [.. catalog
            .Where(k => k.Palette is not null)
            .GroupBy(k => KeyOf(k.Palette))
            .Select(group => new Colorway(group.Key, group.First().Palette!, LabelOf([.. group.Select(k => k.Name)])))];

    public static string KeyOf(KitPalette? palette) => palette is null
        ? string.Empty
        : string.Join("-", new[] { palette.Keys, palette.Legends }.Order(StringComparer.Ordinal).Append(palette.Accent))
            .Replace("#", string.Empty, StringComparison.Ordinal);

    private static string LabelOf(IReadOnlyList<string> names)
    {
        var stems = names.Select(Stem).Distinct(StringComparer.Ordinal).ToList();
        return string.Join(" / ", stems);
    }

    private static string Stem(string name)
    {
        var parenthesis = name.IndexOf(" (", StringComparison.Ordinal);
        return parenthesis > 0 ? name[..parenthesis] : name;
    }

    private static string Fold(string text)
    {
        var folded = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(char.ToLowerInvariant(c));
            }
        }

        return folded.ToString();
    }
}
