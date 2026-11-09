using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Notifications;

internal sealed class EmailOutboxDispatcher(
    AppDbContext db,
    EmailComposer composer,
    IEmailSender sender,
    TimeProvider clock,
    ILogger<EmailOutboxDispatcher> logger) : IEmailOutboxDispatcher
{
    public const int BatchSize = 20;

    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task<int> SendPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow();
        var cutoff = now - Retention;

        await db.OutboxEmails
            .Where(e => e.FinishedAt != null && e.FinishedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var due = await db.OutboxEmails
            .Where(e => e.Status == OutboxEmailStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var sent = 0;
        foreach (var email in due)
        {
            if (await TrySendAsync(email, cancellationToken))
            {
                sent++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return sent;
    }

    private async Task<bool> TrySendAsync(OutboxEmail email, CancellationToken cancellationToken)
    {
        try
        {
            var message = await composer.ComposeAsync(email, cancellationToken);
            if (message is null)
            {
                email.Discard(clock.GetUtcNow());
                logger.LogWarning("E-mail {EmailId} ({Kind}) do pedido {OrderId} descartado: pedido sem dono ou conta sem e-mail", email.Id, email.Kind, email.OrderId);
                return false;
            }

            await sender.SendAsync(message, cancellationToken);
            email.MarkSent(clock.GetUtcNow());
            logger.LogInformation("E-mail {EmailId} ({Kind}) do pedido {OrderId} enviado", email.Id, email.Kind, email.OrderId);
            return true;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            email.RecordFailure(clock.GetUtcNow(), ex is EmailDeliveryException { Permanent: true });

            if (email.Status == OutboxEmailStatus.Failed)
            {
                logger.LogError(ex, "E-mail {EmailId} ({Kind}) do pedido {OrderId} desistido depois de {Attempts} tentativas", email.Id, email.Kind, email.OrderId, email.Attempts);
            }
            else
            {
                logger.LogWarning(ex, "E-mail {EmailId} ({Kind}) do pedido {OrderId} falhou; nova tentativa em {NextAttemptAt}", email.Id, email.Kind, email.OrderId, email.NextAttemptAt);
            }

            return false;
        }
    }
}
