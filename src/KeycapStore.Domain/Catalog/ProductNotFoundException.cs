namespace KeycapStore.Domain.Catalog;

public sealed class ProductNotFoundException(Guid productId)
    : Exception($"O produto {productId} não existe.")
{
    public Guid ProductId { get; } = productId;
}
