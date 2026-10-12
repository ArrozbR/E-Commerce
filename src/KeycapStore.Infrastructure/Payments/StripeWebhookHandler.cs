using KeycapStore.Application.Payments;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using Stripe;
using Stripe.Checkout;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class StripeWebhookHandler(
    AppDbContext db,
    TimeProvider clock,
    IOptions<StripeOptions> options,
    ILogger<StripeWebhookHandler> logger) : IPaymentWebhookHandler
{
    public async Task<WebhookOutcome> HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, options.Value.WebhookSecret);
        }
        catch (StripeException ex)
        {
            logger.LogWarning("Webhook da Stripe recusado: {Reason}", ex.Message);
            return WebhookOutcome.InvalidSignature;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        db.ProcessedStripeEvents.Add(new ProcessedStripeEvent(stripeEvent.Id, stripeEvent.Type, clock.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return WebhookOutcome.Duplicate;
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted && stripeEvent.Data.Object is Session session)
        {
            await ApplyCheckoutCompletedAsync(stripeEvent.Id, session, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return WebhookOutcome.Accepted;
    }

    private async Task ApplyCheckoutCompletedAsync(string eventId, Session session, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(session.Metadata?.GetValueOrDefault("order_id"), out var orderId))
        {
            logger.LogWarning("Evento {EventId} sem order_id nos metadados; ignorado", eventId);
            return;
        }

        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Evento {EventId} aponta para o pedido {OrderId}, que não existe; ignorado", eventId, orderId);
            return;
        }

        if (session.AmountTotal != StripeCheckoutSessionFactory.ToCents(order.Total) || session.Currency != "brl")
        {
            logger.LogError("Evento {EventId}: valor pago diferente do total do pedido {OrderId}; pedido NÃO confirmado", eventId, order.Id);
            return;
        }

        if (session.PaymentStatus == "paid")
        {
            order.ConfirmPayment();
        }
        else if (session.PaymentStatus == "unpaid")
        {
            order.MarkAwaitingConfirmation();
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Evento {EventId}: pedido {OrderId} agora está {Status}", eventId, order.Id, order.Status);
    }
}
