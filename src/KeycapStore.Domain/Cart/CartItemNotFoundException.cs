namespace KeycapStore.Domain.Cart;

public sealed class CartItemNotFoundException(Guid productId)
    : Exception($"O produto {productId} não está no carrinho.")
{
    public Guid ProductId { get; } = productId;
}
