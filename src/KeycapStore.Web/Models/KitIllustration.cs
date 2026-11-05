using System.Globalization;

using KeycapStore.Domain.Catalog;

namespace KeycapStore.Web.Models;

public enum KeyRole
{
    Alpha,
    Modifier,
    Accent,
}

public enum KeyIcon
{
    None,
    Star,
    Heart,
    Moon,
    Bolt,
    Wave,
    Sun,
    Return,
}

public sealed record KeyShape(double X, double Y, double Width, double Height, KeyRole Role, string Legend, KeyIcon Icon = KeyIcon.None);

public sealed record KitIllustration(KitPalette Palette, KitKind Kind)
{
    public const double ViewWidth = 324;
    public const double ViewHeight = 174;

    private const double KeyboardUnit = 26;
    private const double KeyboardGap = 4;
    private const double KitUnit = 44;
    private const double KitGap = 8;

    private const KeyRole A = KeyRole.Alpha;
    private const KeyRole M = KeyRole.Modifier;
    private const KeyRole X = KeyRole.Accent;

    public double Unit => Kind == KitKind.Base ? KeyboardUnit : KitUnit;

    public double RowWidth => Kind == KitKind.Base ? 296 : 304;

    public double Left => (ViewWidth - RowWidth) / 2;

    public IReadOnlyList<KeyShape> Keys => Kind switch
    {
        KitKind.Novelties => Layout(Novelties, KitUnit, KitGap, top: 13),
        KitKind.Modifiers => Layout(Modifiers, KitUnit, KitGap, top: 13),
        KitKind.FunctionKeys => Layout(FunctionKeys, KitUnit, KitGap, top: 13),
        _ => Layout(Keyboard, KeyboardUnit, KeyboardGap, top: 14),
    };

    public string CapColor(KeyRole role) => role switch
    {
        KeyRole.Modifier => Palette.Legends,
        KeyRole.Accent => Palette.Accent,
        _ => Palette.Keys,
    };

    public string LegendColor(KeyRole role) => role switch
    {
        KeyRole.Modifier => Palette.Keys,
        KeyRole.Accent => AccentLegend,
        _ => Palette.Legends,
    };

    public string AccentLegend => Contrast(Palette.Accent, "#0a0a0a") >= Contrast(Palette.Accent, "#ffffff") ? "#0a0a0a" : "#ffffff";

    private static readonly KeySpec[][] Keyboard =
    [
        [K("Esc", 1, X), K("1"), K("2"), K("3"), K("4"), K("5"), K("6"), K("7"), K("8"), K("9")],
        [K("Tab", 1.5, M), K("Q"), K("W"), K("E"), K("R"), K("T"), K("Y"), K("U"), K("I", null)],
        [K("Caps", 1.75, M), K("A"), K("S"), K("D"), K("F"), K("G"), K("H"), K("Enter", null, X)],
        [K("Shift", 2.25, M), K("Z"), K("X"), K("C"), K("V"), K("B"), K("Shift", null, M)],
        [K("Ctrl", 1.5, M), K("Alt", 1.25, M), K("", null, M), K("Alt", 1.25, M), K("Fn", 1.5, M)],
    ];

    private static readonly KeySpec[][] Novelties =
    [
        [I(KeyIcon.Star, 1, X), I(KeyIcon.Heart, 1, A), I(KeyIcon.Moon, 1, M), I(KeyIcon.Bolt, 1, X), I(KeyIcon.Wave, 1, A), I(KeyIcon.Sun, 1, M)],
        [I(KeyIcon.Wave, 2.25, M), I(KeyIcon.Star, 1, A), I(KeyIcon.Heart, null, X)],
        [I(KeyIcon.Moon, 1.5, A), I(KeyIcon.Sun, 1, X), I(KeyIcon.Return, null, M)],
    ];

    private static readonly KeySpec[][] Modifiers =
    [
        [K("Esc", 1, X), K("Tab", 1.5, M), K("Caps", 1.75, M), K("Backspace", null, M)],
        [K("Shift", 2.25, M), K("Ctrl", 1.25, M), K("Win", 1.25, M), K("Enter", null, X)],
        [K("Alt", 1.25, M), K("Fn", 1.25, M), K("Menu", 1.25, M), K("Shift", null, M)],
    ];

    private static readonly KeySpec[][] FunctionKeys =
    [
        [K("Esc", 1, X), K("F1", 1, M), K("F2", 1, M), K("F3", 1, M), K("F4", 1, M), K("F5")],
        [K("F6"), K("F7"), K("F8"), K("F9", 1, M), K("F10", 1, M), K("F11", 1, M)],
        [K("F12", 1, M), K("PrtSc"), K("ScrLk"), K("Pause"), K("Home", 1, M), K("End", 1, M)],
    ];

    private sealed record KeySpec(string Legend, double? Units, KeyRole Role, KeyIcon Icon);

    private static KeySpec K(string legend, double? units = 1, KeyRole role = KeyRole.Alpha) => new(legend, units, role, KeyIcon.None);

    private static KeySpec I(KeyIcon icon, double? units, KeyRole role) => new("", units, role, icon);

    private static double Width(double units, double unit, double gap) => (units * unit) + ((units - 1) * gap);

    private List<KeyShape> Layout(KeySpec[][] rows, double unit, double gap, double top)
    {
        var shapes = new List<KeyShape>();
        for (var r = 0; r < rows.Length; r++)
        {
            var row = rows[r];
            var fixedWidth = row.Where(k => k.Units is not null).Sum(k => Width(k.Units!.Value, unit, gap));
            var fill = RowWidth - fixedWidth - ((row.Length - 1) * gap);
            var x = Left;
            var y = top + (r * (unit + gap));

            foreach (var key in row)
            {
                var width = key.Units is { } units ? Width(units, unit, gap) : fill;
                shapes.Add(new KeyShape(x, y, width, unit, key.Role, key.Legend, key.Icon));
                x += width + gap;
            }
        }

        return shapes;
    }

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
