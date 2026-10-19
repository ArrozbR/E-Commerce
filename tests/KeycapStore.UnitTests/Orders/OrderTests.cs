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

        var ex = Assert.Throws<InvalidOrderTransitionException>(() => order.ConfirmPayment());

        Assert.Equal(OrderStatus.Expired, ex.From);
        Assert.Equal(OrderStatus.Expired, order.Status);
    }

    [Fact]
    public void ConfirmPayment_WhenAlreadyPaid_DoesNothing()
    {
        var order = NewOrder();
        order.ConfirmPayment();

        order.ConfirmPayment();

        Assert.Equal(OrderStatus.Paid, order.Status);
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

    [Fact]
    public void ConfirmLatePayment_FromExpired_GoesToPaid()
    {
        var order = NewOrder();
        order.Expire();

        order.ConfirmLatePayment();

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void ConfirmLatePayment_FromFailed_GoesToPaid()
    {
        var order = NewOrder();
        order.MarkFailed();

        order.ConfirmLatePayment();

        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void ConfirmLatePayment_WhenAwaitingPayment_Throws()
    {
        var order = NewOrder();

        var ex = Assert.Throws<InvalidOrderTransitionException>(() => order.ConfirmLatePayment());

        Assert.Equal(OrderStatus.AwaitingPayment, ex.From);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
    }

    [Fact]
    public void MarkOutOfStock_FromExpired_GoesToPaidOutOfStock_AndKeepsThePaymentIntent()
    {
        var order = NewOrder();
        order.Expire();

        order.MarkOutOfStock("pi_test_123");

        Assert.Equal(OrderStatus.PaidOutOfStock, order.Status);
        Assert.Equal("pi_test_123", order.StripePaymentIntentId);
    }

    [Fact]
    public void MarkOutOfStock_FromFailed_GoesToPaidOutOfStock()
    {
        var order = NewOrder();
        order.MarkFailed();

        order.MarkOutOfStock("pi_test_123");

        Assert.Equal(OrderStatus.PaidOutOfStock, order.Status);
    }

    [Fact]
    public void MarkOutOfStock_WhenPaid_Throws()
    {
        var order = NewOrder();
        order.ConfirmPayment();

        var ex = Assert.Throws<InvalidOrderTransitionException>(() => order.MarkOutOfStock("pi_test_123"));

        Assert.Equal(OrderStatus.Paid, ex.From);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.StripePaymentIntentId);
    }

    [Fact]
    public void MarkOutOfStock_WithoutPaymentIntent_Throws()
    {
        var order = NewOrder();
        order.Expire();

        Assert.Throws<ArgumentException>(() => order.MarkOutOfStock(" "));
        Assert.Equal(OrderStatus.Expired, order.Status);
    }

    [Fact]
    public void MarkRefunded_FromPaidOutOfStock_GoesToRefunded()
    {
        var order = NewOrder();
        order.Expire();
        order.MarkOutOfStock("pi_test_123");

        order.MarkRefunded();

        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    [Fact]
    public void MarkRefunded_WhenPaid_Throws()
    {
        var order = NewOrder();
        order.ConfirmPayment();

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.MarkRefunded);

        Assert.Equal(OrderStatus.Paid, ex.From);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    private static Order PaidOrder()
    {
        var order = NewOrder();
        order.ConfirmPayment();
        return order;
    }

    [Fact]
    public void StartPicking_FromPaid_GoesToPicking()
    {
        var order = PaidOrder();

        order.StartPicking();

        Assert.Equal(OrderStatus.Picking, order.Status);
    }

    [Fact]
    public void StartPicking_WhenAwaitingPayment_Throws()
    {
        var order = NewOrder();

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.StartPicking);

        Assert.Equal(OrderStatus.AwaitingPayment, ex.From);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
    }

    [Fact]
    public void MarkShipped_FromPicking_GoesToShipped_AndKeepsTheTrackingCode()
    {
        var order = PaidOrder();
        order.StartPicking();

        order.MarkShipped("BR123456789");

        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.Equal("BR123456789", order.TrackingCode);
    }

    [Fact]
    public void MarkShipped_WhenNotPickedYet_Throws()
    {
        var order = PaidOrder();

        var ex = Assert.Throws<InvalidOrderTransitionException>(() => order.MarkShipped("BR123456789"));

        Assert.Equal(OrderStatus.Paid, ex.From);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Null(order.TrackingCode);
    }

    [Fact]
    public void MarkShipped_WithoutTrackingCode_Throws()
    {
        var order = PaidOrder();
        order.StartPicking();

        Assert.Throws<ArgumentException>(() => order.MarkShipped(" "));
        Assert.Equal(OrderStatus.Picking, order.Status);
    }

    [Fact]
    public void MarkShipped_TrackingCodeAtTheLimit_IsAccepted_AndOneMoreIsNot()
    {
        var accepted = PaidOrder();
        accepted.StartPicking();
        var refused = PaidOrder();
        refused.StartPicking();

        accepted.MarkShipped(new string('A', Order.MaxTrackingCodeLength));

        Assert.Equal(OrderStatus.Shipped, accepted.Status);
        Assert.Throws<ArgumentOutOfRangeException>(() => refused.MarkShipped(new string('A', Order.MaxTrackingCodeLength + 1)));
        Assert.Equal(OrderStatus.Picking, refused.Status);
    }

    [Fact]
    public void MarkDelivered_FromShipped_GoesToDelivered()
    {
        var order = PaidOrder();
        order.StartPicking();
        order.MarkShipped("BR123456789");

        order.MarkDelivered(DateTimeOffset.UnixEpoch);

        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void MarkDelivered_WhenPicking_Throws()
    {
        var order = PaidOrder();
        order.StartPicking();

        var ex = Assert.Throws<InvalidOrderTransitionException>(() => order.MarkDelivered(DateTimeOffset.UnixEpoch));

        Assert.Equal(OrderStatus.Picking, ex.From);
        Assert.Equal(OrderStatus.Picking, order.Status);
    }

    [Fact]
    public void ConfirmPayment_KeepsThePaymentIntent()
    {
        var order = NewOrder();

        order.ConfirmPayment("pi_test_123");

        Assert.Equal("pi_test_123", order.StripePaymentIntentId);
    }

    [Fact]
    public void ConfirmLatePayment_KeepsThePaymentIntent()
    {
        var order = NewOrder();
        order.Expire();

        order.ConfirmLatePayment("pi_test_456");

        Assert.Equal("pi_test_456", order.StripePaymentIntentId);
    }

    [Fact]
    public void Cancel_FromPaid_GoesToCancelled()
    {
        var order = PaidOrder();

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_FromPicking_GoesToCancelled()
    {
        var order = PaidOrder();
        order.StartPicking();

        order.Cancel();

        Assert.Equal(OrderStatus.Cancelled, order.Status);
    }

    [Fact]
    public void Cancel_WhenShipped_Throws()
    {
        var order = PaidOrder();
        order.StartPicking();
        order.MarkShipped("BR123456789");

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.Cancel);

        Assert.Equal(OrderStatus.Shipped, ex.From);
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Fact]
    public void Cancel_WhenAwaitingPayment_Throws()
    {
        var order = NewOrder();

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.Cancel);

        Assert.Equal(OrderStatus.AwaitingPayment, ex.From);
    }

    [Fact]
    public void MarkRefunded_FromCancelled_GoesToRefunded()
    {
        var order = PaidOrder();
        order.Cancel();

        order.MarkRefunded();

        Assert.Equal(OrderStatus.Refunded, order.Status);
    }

    private static readonly DateTimeOffset DeliveryDay = new(2026, 10, 20, 15, 0, 0, TimeSpan.Zero);

    private static Order ShippedOrder()
    {
        var order = PaidOrder();
        order.StartPicking();
        order.MarkShipped("BR123456789");
        return order;
    }

    private static Order DeliveredOrder()
    {
        var order = ShippedOrder();
        order.MarkDelivered(DeliveryDay);
        return order;
    }

    [Fact]
    public void MarkDelivered_RecordsWhen()
    {
        var order = DeliveredOrder();

        Assert.Equal(DeliveryDay, order.DeliveredAt);
    }

    [Fact]
    public void Withdraw_WhileShipped_GoesToWithdrawn()
    {
        var order = ShippedOrder();

        order.Withdraw(DeliveryDay);

        Assert.Equal(OrderStatus.Withdrawn, order.Status);
    }

    [Fact]
    public void Withdraw_OnTheLastMomentOfTheSevenDays_IsAccepted()
    {
        var order = DeliveredOrder();

        order.Withdraw(DeliveryDay + Order.WithdrawalPeriod);

        Assert.Equal(OrderStatus.Withdrawn, order.Status);
    }

    [Fact]
    public void Withdraw_AfterTheSevenDays_Throws()
    {
        var order = DeliveredOrder();

        var ex = Assert.Throws<InvalidOrderTransitionException>(() => order.Withdraw(DeliveryDay + Order.WithdrawalPeriod + TimeSpan.FromSeconds(1)));

        Assert.Equal(OrderStatus.Delivered, ex.From);
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void Withdraw_BeforeShipping_Throws()
    {
        var order = PaidOrder();

        var ex = Assert.Throws<InvalidOrderTransitionException>(() => order.Withdraw(DeliveryDay));

        Assert.Equal(OrderStatus.Paid, ex.From);
        Assert.Equal(OrderStatus.Paid, order.Status);
    }

    [Fact]
    public void MarkRefunded_FromWithdrawn_WaitsForTheProductToComeBack()
    {
        var order = DeliveredOrder();
        order.Withdraw(DeliveryDay);

        order.MarkRefunded();

        Assert.Equal(OrderStatus.AwaitingReturn, order.Status);
    }

    [Fact]
    public void ConfirmReturn_AfterTheRefund_GoesToReturned()
    {
        var order = DeliveredOrder();
        order.Withdraw(DeliveryDay);
        order.MarkRefunded();

        order.ConfirmReturn();

        Assert.Equal(OrderStatus.Returned, order.Status);
    }

    [Fact]
    public void ConfirmReturn_BeforeTheRefund_Throws()
    {
        var order = DeliveredOrder();
        order.Withdraw(DeliveryDay);

        var ex = Assert.Throws<InvalidOrderTransitionException>(order.ConfirmReturn);

        Assert.Equal(OrderStatus.Withdrawn, ex.From);
        Assert.Equal(OrderStatus.Withdrawn, order.Status);
    }
}
