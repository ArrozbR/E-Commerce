using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class PaymentReconciler(
    AppDbContext db,
    IPaymentGateway payments,
    CheckoutSessionTransitions transitions,
    TimeProvider clock,
    ILogger<PaymentReconciler> logger) : IPaymentReconciler
{
    public static readonly TimeSpan StuckAge = TimeSpan.FromMinutes(40);

    public const int MaxOrdersPerRound = 50;

    private const string Origin = "Conciliação";

    public async Task<int> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - StuckAge;

        var stuck = await db.Orders
            .Where(o => (o.Status == OrderStatus.AwaitingPayment || o.Status == OrderStatus.AwaitingConfirmation) && o.StripeSessionId != null && o.CreatedAt < cutoff)
            .OrderBy(o => o.CreatedAt)
            .Take(MaxOrdersPerRound)
            .Select(o => new { o.Id, o.StripeSessionId })
            .ToListAsync(cancellationToken);

        var corrected = 0;
        foreach (var candidate in stuck)
        {
            try
            {
                var session = await payments.GetCheckoutSessionAsync(candidate.StripeSessionId!, cancellationToken);

                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                var order = await db.Orders.SingleAsync(o => o.Id == candidate.Id, cancellationToken);
                var before = order.Status;

                if (session.Status == "expired")
                {
                    await transitions.ApplyExpiredAsync(Origin, order, cancellationToken);
                }
                else if (session.PaymentStatus == "paid")
                {
                    await transitions.ApplyPaymentAsync(Origin, order, session, cancellationToken);
                }
                else
                {
                    logger.LogInformation("{Origin}: sessão do pedido {OrderId} ainda está {SessionStatus}/{PaymentStatus}; nada a fazer", Origin, order.Id, session.Status, session.PaymentStatus);
                    continue;
                }

                await transaction.CommitAsync(cancellationToken);

                if (order.Status != before)
                {
                    corrected++;
                    logger.LogWarning("{Origin}: pedido {OrderId} passou de {Before} para {Status}; um evento do webhook se perdeu", Origin, order.Id, before, order.Status);
                }
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();

                logger.LogInformation("{Origin}: pedido {OrderId} mudou durante a conferência; fica para a próxima rodada", Origin, candidate.Id);
            }
            catch (PaymentGatewayException ex)
            {
                logger.LogWarning(ex, "{Origin}: não foi possível consultar a sessão do pedido {OrderId}; fica para a próxima rodada", Origin, candidate.Id);
            }
        }

        return corrected;
    }
}
