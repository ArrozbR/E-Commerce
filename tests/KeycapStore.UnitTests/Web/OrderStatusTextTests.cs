using KeycapStore.Domain.Orders;
using KeycapStore.Web.Models;

namespace KeycapStore.UnitTests.Web;

public class OrderStatusTextTests
{
    public static TheoryData<OrderStatus> EveryStatus() => [.. Enum.GetValues<OrderStatus>()];

    [Theory]
    [MemberData(nameof(EveryStatus))]
    public void EveryStatus_IsOnTheProgressTrail_OrExplainsWhatHappened(OrderStatus status)
    {
        Assert.True(OrderStatusText.Step(status) is not null ^ OrderStatusText.Explanation(status) is not null);
        Assert.NotEqual(status.ToString(), OrderStatusText.For(status));
    }

    [Theory]
    [InlineData(OrderStatus.AwaitingPayment, 1, "warning")]
    [InlineData(OrderStatus.Paid, 2, "success")]
    [InlineData(OrderStatus.Picking, 3, "success")]
    [InlineData(OrderStatus.Shipped, 4, "success")]
    [InlineData(OrderStatus.Delivered, 5, "success")]
    public void TheNormalPath_GoesStepByStep(OrderStatus status, int step, string tone)
    {
        Assert.Equal(step, OrderStatusText.Step(status));
        Assert.Equal(tone, OrderStatusText.Tone(status));
    }

    [Fact]
    public void FailedPayment_IsTheOnlyRedStatus()
    {
        Assert.Equal([OrderStatus.Failed], Enum.GetValues<OrderStatus>().Where(s => OrderStatusText.Tone(s) == "danger"));
        Assert.Equal("badge badge-danger", OrderStatusText.BadgeClass(OrderStatus.Failed));
    }
}
