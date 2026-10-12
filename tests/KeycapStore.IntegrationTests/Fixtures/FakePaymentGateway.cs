using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;

namespace KeycapStore.IntegrationTests.Fixtures;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool Fail { get; set; }

    public List<Guid> RequestedOrders { get; } = [];

    public Task<CheckoutSession> CreateCheckoutSessionAsync(Order order, Uri successUrl, Uri cancelUrl, CancellationToken cancellationToken = default)
    {
        RequestedOrders.Add(order.Id);

        if (Fail)
        {
            throw new PaymentGatewayException("Stripe fora do ar (simulado).");
        }

        return Task.FromResult(new CheckoutSession($"cs_test_fake_{order.Id:N}", new Uri($"https://checkout.stripe.test/{order.Id:N}")));
    }
}
