using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class OrderTests
{
    private static Order NewOrder() => new("customer-1", DateTimeOffset.UnixEpoch);

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
