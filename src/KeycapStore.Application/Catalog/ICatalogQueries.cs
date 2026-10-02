namespace KeycapStore.Application.Catalog;

public interface ICatalogQueries
{
    Task<IReadOnlyList<ProductSummary>> ListProductsAsync(CancellationToken cancellationToken = default);
}
