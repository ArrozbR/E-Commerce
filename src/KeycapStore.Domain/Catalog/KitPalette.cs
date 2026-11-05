namespace KeycapStore.Domain.Catalog;

public sealed record KitPalette
{
    public static KitPalette Neutral { get; } = new("#e4e4e7", "#3f3f46", "#a1a1aa");

    public string Keys { get; }
    public string Legends { get; }
    public string Accent { get; }

    public KitPalette(string keys, string legends, string accent)
    {
        Keys = Normalize(keys, nameof(keys));
        Legends = Normalize(legends, nameof(legends));
        Accent = Normalize(accent, nameof(accent));
    }

    public static bool IsHexColor(string? value) =>
        value is { Length: 7 } && value[0] == '#' && value.AsSpan(1).ContainsAnyExcept("0123456789abcdefABCDEF") is false;

    private static string Normalize(string value, string paramName)
    {
        if (!IsHexColor(value))
        {
            throw new ArgumentException("A cor precisa estar no formato #RRGGBB.", paramName);
        }

        return value.ToLowerInvariant();
    }
}
