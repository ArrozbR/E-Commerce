namespace KeycapStore.Domain.Catalog;

public sealed record ProductPackage
{
    public const decimal MaxWeightKg = 30m;
    public const int MaxSideCm = 100;

    public decimal WeightKg { get; }
    public int WidthCm { get; }
    public int HeightCm { get; }
    public int LengthCm { get; }

    public ProductPackage(decimal weightKg, int widthCm, int heightCm, int lengthCm)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weightKg);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(weightKg, MaxWeightKg);
        if (decimal.Round(weightKg, 3) != weightKg)
        {
            throw new ArgumentOutOfRangeException(nameof(weightKg));
        }

        ThrowIfInvalidSide(widthCm, nameof(widthCm));
        ThrowIfInvalidSide(heightCm, nameof(heightCm));
        ThrowIfInvalidSide(lengthCm, nameof(lengthCm));

        WeightKg = weightKg;
        WidthCm = widthCm;
        HeightCm = heightCm;
        LengthCm = lengthCm;
    }

    private static void ThrowIfInvalidSide(int sideCm, string paramName)
    {
        if (sideCm <= 0 || sideCm > MaxSideCm)
        {
            throw new ArgumentOutOfRangeException(paramName);
        }
    }
}
