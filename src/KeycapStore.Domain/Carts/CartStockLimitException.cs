namespace KeycapStore.Domain.Carts;

public sealed class CartStockLimitException(Guid productId, int available)
    : Exception($"O produto {productId} tem só {available} unidades disponíveis.")
{
    public Guid ProductId { get; } = productId;

    public int Available { get; } = available;
}
