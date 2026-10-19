using System.Collections.Concurrent;

using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;

namespace KeycapStore.IntegrationTests.Fixtures;

public sealed class FakePaymentGateway : IPaymentGateway
{
    public bool Fail { get; set; }

    public bool FailRefunds { get; set; }

    public List<Guid> RequestedOrders { get; } = [];

    public ConcurrentQueue<(Guid OrderId, string PaymentIntentId)> Refunds { get; } = [];

    public Task<CheckoutSession> CreateCheckoutSessionAsync(Order order, Uri successUrl, Uri cancelUrl, CancellationToken cancellationToken = default)
    {
        RequestedOrders.Add(order.Id);

        if (Fail)
        {
            throw new PaymentGatewayException("Stripe fora do ar (simulado).");
        }

        return Task.FromResult(new CheckoutSession($"cs_test_fake_{order.Id:N}", new Uri($"https://checkout.stripe.test/{order.Id:N}")));
    }

    public Task RefundAsync(Guid orderId, string paymentIntentId, CancellationToken cancellationToken = default)
    {
        if (FailRefunds)
        {
            throw new PaymentGatewayException("Stripe recusou o reembolso (simulado).");
        }

        Refunds.Enqueue((orderId, paymentIntentId));
        return Task.CompletedTask;
    }

    public Task<string> GetPaymentIntentIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult($"pi_from_{checkoutSessionId}");
    }
}
