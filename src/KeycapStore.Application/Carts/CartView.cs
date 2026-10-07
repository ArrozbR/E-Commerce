namespace KeycapStore.Application.Carts;

public sealed record CartLineView(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

public sealed record CartView(IReadOnlyList<CartLineView> Lines)
{
    public static CartView Empty { get; } = new([]);

    public decimal Total => Lines.Sum(l => l.LineTotal);
}
