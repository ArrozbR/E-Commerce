namespace KeycapStore.Domain.Carts;

public sealed class CartItem
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    internal CartItem(Guid productId, int quantity)
    {
        ProductId = productId;
        Quantity = quantity;
    }

    internal void SetQuantity(int quantity) => Quantity = quantity;
}
