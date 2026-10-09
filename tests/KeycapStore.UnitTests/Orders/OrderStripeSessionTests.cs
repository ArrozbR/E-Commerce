using KeycapStore.Domain.Orders;

namespace KeycapStore.UnitTests.Orders;

public class OrderStripeSessionTests
{
    private static readonly ShippingAddress SaoPaulo =
        new("Ana Souza", "Avenida Paulista", "1000", null, "Bela Vista", "São Paulo", "SP", "01310100");

    private static Order NewOrder() =>
        new("customer-1", DateTimeOffset.UnixEpoch, [new OrderItem(Guid.NewGuid(), "Kit A", 100m, 1)], SaoPaulo);

    [Fact]
    public void NewOrder_HasNoStripeSession()
    {
        var order = NewOrder();

        Assert.Null(order.StripeSessionId);
    }

    [Fact]
    public void AttachStripeSession_KeepsTheSessionId()
    {
        var order = NewOrder();

        order.AttachStripeSession("cs_test_abc");

        Assert.Equal("cs_test_abc", order.StripeSessionId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AttachStripeSession_WithBlankId_Throws(string sessionId)
    {
        var order = NewOrder();

        Assert.Throws<ArgumentException>(() => order.AttachStripeSession(sessionId));
        Assert.Null(order.StripeSessionId);
    }

    [Fact]
    public void AttachStripeSession_SameIdTwice_IsAccepted()
    {
        var order = NewOrder();
        order.AttachStripeSession("cs_test_abc");

        order.AttachStripeSession("cs_test_abc");

        Assert.Equal("cs_test_abc", order.StripeSessionId);
    }

    [Fact]
    public void AttachStripeSession_DifferentId_Throws_AndKeepsTheFirst()
    {
        var order = NewOrder();
        order.AttachStripeSession("cs_test_abc");

        Assert.Throws<InvalidOperationException>(() => order.AttachStripeSession("cs_test_xyz"));
        Assert.Equal("cs_test_abc", order.StripeSessionId);
    }

    [Fact]
    public void AttachStripeSession_WhenNotAwaitingPayment_Throws()
    {
        var order = NewOrder();
        order.MarkFailed();

        Assert.Throws<InvalidOperationException>(() => order.AttachStripeSession("cs_test_abc"));
        Assert.Null(order.StripeSessionId);
    }
}
