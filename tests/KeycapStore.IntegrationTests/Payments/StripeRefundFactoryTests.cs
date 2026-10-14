using KeycapStore.Infrastructure.Payments;

namespace KeycapStore.IntegrationTests.Payments;

public class StripeRefundFactoryTests
{
    [Fact]
    public void Refund_TargetsThePaymentIntent()
    {
        Assert.Equal("pi_test_123", StripeRefundFactory.BuildOptions("pi_test_123").PaymentIntent);
    }

    [Fact]
    public void Refund_UsesADeterministicIdempotencyKeyPerOrder()
    {
        var orderId = Guid.NewGuid();

        Assert.Equal($"refund-{orderId}", StripeRefundFactory.BuildRequestOptions(orderId).IdempotencyKey);
    }
}
