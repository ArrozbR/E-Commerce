using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Catalog;

namespace KeycapStore.Application.Carts;

public sealed record CartLineView(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    bool IsAvailable,
    int AvailableStock,
    DateTimeOffset? PreOrderShipsAt = null,
    KitPalette? Palette = null,
    KitKind KitKind = KitKind.Base)
{
    public decimal LineTotal => UnitPrice * Quantity;

    public int MaxQuantity => Math.Min(Cart.MaxQuantityPerItem, Math.Max(1, AvailableStock));
}

public sealed record CartView(IReadOnlyList<CartLineView> Lines)
{
    public static CartView Empty { get; } = new([]);

    public decimal Total => Lines.Sum(l => l.LineTotal);

    public int Units => Lines.Sum(l => l.Quantity);

    public bool HasUnavailableItems => Lines.Any(l => !l.IsAvailable);
}
