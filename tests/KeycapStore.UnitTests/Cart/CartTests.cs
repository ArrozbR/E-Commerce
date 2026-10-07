using KeycapStore.Domain.Cart;

using CartEntity = KeycapStore.Domain.Cart.Cart;

namespace KeycapStore.UnitTests.Cart;

public class CartTests
{
    private static readonly Guid KitA = Guid.NewGuid();
    private static readonly Guid KitB = Guid.NewGuid();

    private static CartEntity NewCart() => new("customer-1");

    [Fact]
    public void AddItem_WithNewProduct_AddsOneLine()
    {
        var cart = NewCart();

        cart.AddItem(KitA, 2);

        var item = Assert.Single(cart.Items);
        Assert.Equal(KitA, item.ProductId);
        Assert.Equal(2, item.Quantity);
    }

    [Fact]
    public void AddItem_WithSameProductTwice_SumsQuantities()
    {
        var cart = NewCart();

        cart.AddItem(KitA, 2);
        cart.AddItem(KitA, 3);

        var item = Assert.Single(cart.Items);
        Assert.Equal(5, item.Quantity);
    }

    [Fact]
    public void AddItem_WithZeroQuantity_ThrowsAndKeepsCartEmpty()
    {
        var cart = NewCart();

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.AddItem(KitA, 0));

        Assert.Empty(cart.Items);
    }

    [Fact]
    public void ChangeQuantity_WithProductInCart_ReplacesQuantity()
    {
        var cart = NewCart();
        cart.AddItem(KitA, 1);

        cart.ChangeQuantity(KitA, 10);

        var item = Assert.Single(cart.Items);
        Assert.Equal(10, item.Quantity);
    }

    [Fact]
    public void ChangeQuantity_WithProductNotInCart_ThrowsCartItemNotFound()
    {
        var cart = NewCart();

        var ex = Assert.Throws<CartItemNotFoundException>(() => cart.ChangeQuantity(KitA, 5));

        Assert.Equal(KitA, ex.ProductId);
        Assert.Empty(cart.Items);
    }

    [Fact]
    public void RemoveItem_WithProductInCart_RemovesOnlyThatProduct()
    {
        var cart = NewCart();
        cart.AddItem(KitA, 3);
        cart.AddItem(KitB, 1);

        cart.RemoveItem(KitA);

        var remaining = Assert.Single(cart.Items);
        Assert.Equal(KitB, remaining.ProductId);
    }

    [Fact]
    public void RemoveItem_WithProductNotInCart_DoesNothing()
    {
        var cart = NewCart();
        cart.AddItem(KitB, 1);

        cart.RemoveItem(KitA);

        var remaining = Assert.Single(cart.Items);
        Assert.Equal(KitB, remaining.ProductId);
    }
}
