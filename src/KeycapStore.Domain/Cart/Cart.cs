namespace KeycapStore.Domain.Cart;

public sealed class Cart
{
    private readonly List<CartItem> _items = [];

    public Guid Id { get; private set; }
    public string CustomerId { get; private set; }
    public IReadOnlyList<CartItem> Items => _items;

    public Cart(string customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);

        Id = Guid.NewGuid();
        CustomerId = customerId;
    }

    public void AddItem(Guid productId, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var item = _items.Find(i => i.ProductId == productId);
        if (item is null)
        {
            _items.Add(new CartItem(productId, quantity));
            return;
        }

        item.SetQuantity(item.Quantity + quantity);
    }

    public void ChangeQuantity(Guid productId, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var item = _items.Find(i => i.ProductId == productId);
        if (item is null)
        {
            throw new CartItemNotFoundException(productId);
        }

        item.SetQuantity(quantity);
    }

    public void RemoveItem(Guid productId)
    {
        _items.RemoveAll(i => i.ProductId == productId);
    }
}
