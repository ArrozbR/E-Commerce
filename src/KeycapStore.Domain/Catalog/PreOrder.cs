namespace KeycapStore.Domain.Catalog;

public sealed record PreOrder
{
    public static readonly TimeSpan MaxLeadTime = TimeSpan.FromDays(80);

    public DateTimeOffset EndsAt { get; }
    public DateTimeOffset ShipsAt { get; }
    public int MinimumUnits { get; }

    public PreOrder(DateTimeOffset endsAt, DateTimeOffset shipsAt, int minimumUnits)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(shipsAt, endsAt, nameof(shipsAt));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumUnits);

        EndsAt = endsAt.ToUniversalTime();
        ShipsAt = shipsAt.ToUniversalTime();
        MinimumUnits = minimumUnits;
    }

    public bool IsOpenAt(DateTimeOffset now) => now < EndsAt;
}
