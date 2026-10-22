namespace KeycapStore.Application.Carts;

public sealed record CartLineView(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, bool IsAvailable)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

public sealed record CartView(IReadOnlyList<CartLineView> Lines)
{
    public static CartView Empty { get; } = new([]);

    public decimal Total => Lines.Sum(l => l.LineTotal);

    public bool HasUnavailableItems => Lines.Any(l => !l.IsAvailable);
}
