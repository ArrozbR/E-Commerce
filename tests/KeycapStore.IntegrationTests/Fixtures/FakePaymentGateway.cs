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

    public bool FailSessionLookups { get; set; }

    public ConcurrentDictionary<string, CheckoutSessionState> Sessions { get; } = [];

    public ConcurrentQueue<string> SessionLookups { get; } = [];

    public ConcurrentQueue<(Guid OrderId, string CustomerEmail, Uri ReturnUrl)> SessionRequests { get; } = [];

    public static string ClientSecretFor(string sessionId) => $"{sessionId}_secret_de_teste";

    public Task<CheckoutSession> CreateCheckoutSessionAsync(Order order, string customerEmail, Uri returnUrl, CancellationToken cancellationToken = default)
    {
        RequestedOrders.Add(order.Id);
        SessionRequests.Enqueue((order.Id, customerEmail, returnUrl));

        if (Fail)
        {
            throw new PaymentGatewayException("Stripe fora do ar (simulado).");
        }

        var sessionId = $"cs_test_fake_{order.Id:N}";
        return Task.FromResult(new CheckoutSession(sessionId, ClientSecretFor(sessionId)));
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

    public Task<CheckoutSessionState> GetCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutSessionId);
        SessionLookups.Enqueue(checkoutSessionId);

        if (FailSessionLookups)
        {
            throw new PaymentGatewayException("Stripe fora do ar (simulado).");
        }

        return Task.FromResult(Sessions.TryGetValue(checkoutSessionId, out var state)
            ? state
            : new CheckoutSessionState("open", "unpaid", null, null, null, ClientSecretFor(checkoutSessionId)));
    }
}
