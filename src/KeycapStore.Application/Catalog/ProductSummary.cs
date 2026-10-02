namespace KeycapStore.Application.Catalog;

public sealed record ProductSummary(Guid Id, string Name, decimal Price, int Stock);
