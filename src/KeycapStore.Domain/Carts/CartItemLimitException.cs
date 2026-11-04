namespace KeycapStore.Domain.Carts;

public sealed class CartItemLimitException(Guid productId)
    : Exception($"O produto {productId} passaria de {Cart.MaxQuantityPerItem} unidades no carrinho.")
{
    public Guid ProductId { get; } = productId;
}
