using KeycapStore.Domain.Catalog;

namespace KeycapStore.Application.Catalog;

public sealed record AdminProductView(Guid Id, string Name, decimal Price, int Stock, bool IsActive, decimal WeightKg, int WidthCm, int HeightCm, int LengthCm, PreOrder? PreOrder = null);

public interface IProductAdminService
{
    Task<IReadOnlyList<AdminProductView>> ListAsync(CancellationToken cancellationToken = default);

    Task<Guid> CreateAsync(string name, decimal price, int stock, ProductPackage package, CancellationToken cancellationToken = default);

    Task<Guid> CreatePreOrderAsync(string name, decimal price, int slots, ProductPackage package, PreOrder preOrder, CancellationToken cancellationToken = default);

    Task UpdateAsync(Guid productId, string name, decimal price, ProductPackage package, CancellationToken cancellationToken = default);

    Task<StockAdjustmentResult> AdjustStockAsync(Guid productId, int delta, CancellationToken cancellationToken = default);

    Task ActivateAsync(Guid productId, CancellationToken cancellationToken = default);

    Task DeactivateAsync(Guid productId, CancellationToken cancellationToken = default);
}
