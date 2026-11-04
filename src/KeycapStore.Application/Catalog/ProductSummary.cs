namespace KeycapStore.Application.Catalog;

public sealed record PreOrderSummary(DateTimeOffset EndsAt, DateTimeOffset ShipsAt, int MinimumUnits, int SoldUnits, bool IsOpen)
{
    public bool MinimumReached => SoldUnits >= MinimumUnits;
}

public sealed record ProductSummary(Guid Id, string Name, decimal Price, int Stock, PreOrderSummary? PreOrder = null)
{
    public bool CanBeBought => Stock > 0 && (PreOrder is null || PreOrder.IsOpen);
}
