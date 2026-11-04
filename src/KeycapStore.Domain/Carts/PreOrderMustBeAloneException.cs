namespace KeycapStore.Domain.Carts;

public sealed class PreOrderMustBeAloneException(Guid productId)
    : Exception($"O produto {productId} não pode ir no mesmo carrinho: pré-venda é comprada sozinha.")
{
    public Guid ProductId { get; } = productId;
}
