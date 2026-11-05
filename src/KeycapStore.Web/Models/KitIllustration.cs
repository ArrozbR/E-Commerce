using System.Globalization;

using KeycapStore.Domain.Catalog;

namespace KeycapStore.Web.Models;

public sealed record KitIllustration(KitPalette Palette, string ProductName)
{
    private const string KitPrefix = "Kit ";

    public string Initial
    {
        get
        {
            var name = ProductName.StartsWith(KitPrefix, StringComparison.OrdinalIgnoreCase)
                ? ProductName[KitPrefix.Length..]
                : ProductName;
            var first = name.TrimStart().FirstOrDefault(char.IsLetterOrDigit);

            return first == default ? "K" : char.ToUpper(first, CultureInfo.InvariantCulture).ToString();
        }
    }

    public string AccentLegend => Contrast(Palette.Accent, "#0a0a0a") >= Contrast(Palette.Accent, "#ffffff") ? "#0a0a0a" : "#ffffff";

    private static double Contrast(string first, string second)
    {
        var a = Luminance(first);
        var b = Luminance(second);

        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string hex)
    {
        static double Channel(string hex, int start)
        {
            var value = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return value <= 0.03928 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(hex, 1)) + (0.7152 * Channel(hex, 3)) + (0.0722 * Channel(hex, 5));
    }
}
