using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;

using Microsoft.Extensions.Options;

using Stripe;
using Stripe.Checkout;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    private readonly StripeClient _client = new(options.Value.SecretKey);

    public async Task<CheckoutSession> CreateCheckoutSessionAsync(Order order, Uri successUrl, Uri cancelUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await new SessionService(_client).CreateAsync(
                StripeCheckoutSessionFactory.BuildOptions(order, successUrl, cancelUrl),
                StripeCheckoutSessionFactory.BuildRequestOptions(order),
                cancellationToken);

            return new CheckoutSession(session.Id, new Uri(session.Url));
        }
        catch (StripeException ex)
        {
            throw new PaymentGatewayException("A Stripe recusou criar a sessão de pagamento.", ex);
        }
    }

    public async Task RefundAsync(Guid orderId, string paymentIntentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await new RefundService(_client).CreateAsync(
                StripeRefundFactory.BuildOptions(paymentIntentId),
                StripeRefundFactory.BuildRequestOptions(orderId),
                cancellationToken);
        }
        catch (StripeException ex)
        {
            throw new PaymentGatewayException("A Stripe recusou o reembolso.", ex);
        }
    }

    public async Task<string> GetPaymentIntentIdAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await new SessionService(_client).GetAsync(checkoutSessionId, cancellationToken: cancellationToken);

            return session.PaymentIntentId
                ?? throw new PaymentGatewayException("A sessão da Stripe não tem pagamento.");
        }
        catch (StripeException ex)
        {
            throw new PaymentGatewayException("A Stripe recusou a consulta da sessão.", ex);
        }
    }

    public async Task<CheckoutSessionState> GetCheckoutSessionAsync(string checkoutSessionId, CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await new SessionService(_client).GetAsync(checkoutSessionId, cancellationToken: cancellationToken);

            return CheckoutSessionTransitions.StateOf(session);
        }
        catch (StripeException ex)
        {
            throw new PaymentGatewayException("A Stripe recusou a consulta da sessão.", ex);
        }
    }
}
