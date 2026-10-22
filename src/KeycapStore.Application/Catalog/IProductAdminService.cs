namespace KeycapStore.Application.Catalog;

public sealed record AdminProductView(Guid Id, string Name, decimal Price, int Stock, bool IsActive);

public interface IProductAdminService
{
    Task<IReadOnlyList<AdminProductView>> ListAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(string name, decimal price, int stock, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid productId, string name, decimal price, CancellationToken cancellationToken = default);

    Task<StockAdjustmentResult> AdjustStockAsync(Guid productId, int delta, CancellationToken cancellationToken = default);

    Task ActivateAsync(Guid productId, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid productId, CancellationToken cancellationToken = default);
}
