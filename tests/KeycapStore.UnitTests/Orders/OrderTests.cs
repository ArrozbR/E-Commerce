using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class OrderTests
{
    private static readonly Guid KitA = Guid.NewGuid();
    private static readonly Guid KitB = Guid.NewGuid();

    private static readonly ShippingAddress SaoPaulo =
        new("Ana Souza", "Avenida Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", "01310100");
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static Order NewOrder() =>
        new("customer-1", DateTimeOffset.UnixEpoch, [new OrderItem(KitA, "Kit A", 100m, 1)], SaoPaulo);

    [Fact]
    public void NewOrder_KeepsItemsAndSumsTheTotal()
    {
        var order = new Order("customer-1", DateTimeOffset.UnixEpoch,
        [
            new OrderItem(KitA, "Kit A", 349.90m, 2),
            new OrderItem(KitB, "Kit B", 89.90m, 1),
        ], SaoPaulo);

        Assert.Equal(2, order.Items.Count);
        Assert.Equal(789.70m, order.Total);
    }

    [Fact]
    public void NewOrder_ToFreeShippingState_HasNoShippingFee()
    {
        var order = NewOrder();

        Assert.Equal(0m, order.ShippingFee);
        Assert.Equal(100m, order.Total);
        Assert.Same(SaoPaulo, order.ShippingAddress);
    }

    [Fact]
    public void NewOrder_ToOtherState_AddsTheShippingFeeToTheTotal()
    {
        var order = new Order("customer-1", DateTimeOffset.UnixEpoch, [new OrderItem(KitA, "Kit A", 100m, 1)], Curitiba);

        Assert.Equal(15.00m, order.ShippingFee);
        Assert.Equal(115.00m, order.Total);
    }

    [Fact]
    public void NewOrder_WithoutShippingAddress_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Order("customer-1", DateTimeOffset.UnixEpoch, [new OrderItem(KitA, "Kit A", 100m, 1)], null!));
    }

    [Fact]
    public void NewOrder_WithoutItems_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Order("customer-1", DateTimeOffset.UnixEpoch, [], SaoPaulo));
    }

    [Fact]
    public void NewOrder_WithSameProductTwice_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Order("customer-1", DateTimeOffset.UnixEpoch,
        [
            new OrderItem(KitA, "Kit A", 100m, 1),
            new OrderItem(KitA, "Kit A", 100m, 2),
        ], SaoPaulo));
    }

    [Fact]
    public void NewOrder_ChangingTheOriginalListLater_DoesNotChangeTheOrder()
    {
        var items = new List<OrderItem> { new(KitA, "Kit A", 100m, 1) };
        var order = new Order("customer-1", DateTimeOffset.UnixEpoch, items, SaoPaulo);

        items.Add(new OrderItem(KitB, "Kit B", 50m, 1));

        Assert.Single(order.Items);
    }


    [Fact]
    public void NewOrder_StartsAwaitingPayment()
    {
        Assert.Equal(OrderStatus.AwaitingPayment, NewOrder().Status);
    }

    [Fact]
    public void ConfirmPayment_FromAwaitingPayment_GoesToPaid()
    {
        var order = NewOrder();

        order.ConfirmPayment();

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void ConfirmPayment_WhenExpired_Throws()
    {
        var order = NewOrder();
        order.Expire();

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.ConfirmPayment);

        Assert.Equal(OrderStatus.Expired, ex.From);
        Assert.Equal(OrderStatus.Expired, order.Status);
    }

    [Fact]
    public void ConfirmPayment_FromAwaitingConfirmation_GoesToPaid()
    {
        var order = NewOrder();
        order.MarkAwaitingConfirmation();

        order.ConfirmPayment();

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void MarkAwaitingConfirmation_FromAwaitingPayment_GoesToAwaitingConfirmation()
    {
        var order = NewOrder();

        order.MarkAwaitingConfirmation();

        Assert.Equal(OrderStatus.AwaitingConfirmation, order.Status);
    }

    [Fact]
    public void MarkAwaitingConfirmation_WhenPaid_Throws()
    {
        var order = NewOrder();
        order.ConfirmPayment();

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.MarkAwaitingConfirmation);

        Assert.Equal(OrderStatus.Paid, ex.From);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void Expire_FromAwaitingPayment_GoesToExpired()
    {
        var order = NewOrder();

        order.Expire();

        Assert.Equal(OrderStatus.Expired, order.Status);
    }

    [Fact]
    public void Expire_WhenPaid_Throws()
    {
        var order = NewOrder();
        order.ConfirmPayment();

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.Expire);

        Assert.Equal(OrderStatus.Paid, ex.From);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void Expire_WhenAwaitingConfirmation_Throws()
    {
        var order = NewOrder();
        order.MarkAwaitingConfirmation();

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.Expire);

        Assert.Equal(OrderStatus.AwaitingConfirmation, ex.From);
        Assert.Equal(OrderStatus.AwaitingConfirmation, order.Status);
    }

    [Fact]
    public void MarkFailed_FromAwaitingPayment_GoesToFailed()
    {
        var order = NewOrder();

        order.MarkFailed();

        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Fact]
    public void MarkFailed_FromAwaitingConfirmation_GoesToFailed()
    {
        var order = NewOrder();
        order.MarkAwaitingConfirmation();

        order.MarkFailed();

        Assert.Equal(OrderStatus.Failed, order.Status);
    }

    [Fact]
    public void MarkFailed_WhenPaid_Throws()
    {
        var order = NewOrder();
        order.ConfirmPayment();

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.MarkFailed);

        Assert.Equal(OrderStatus.Paid, ex.From);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }
}
