using KeycapStore.Application.Payments;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using Npgsql;

using Stripe;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class StripeWebhookHandler(AppDbContext db, TimeProvider clock, IOptions<StripeOptions> options) : IPaymentWebhookHandler
{
    public async Task<WebhookOutcome> HandleAsync(string payload, string signatureHeader, CancellationToken cancellationToken = default)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, options.Value.WebhookSecret);
        }
        catch (StripeException)
        {
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

        await transaction.CommitAsync(cancellationToken);

        return WebhookOutcome.Accepted;
    }
}
