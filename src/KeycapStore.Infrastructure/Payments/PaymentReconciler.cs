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
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var corrected = 0;
        foreach (var orderId in stuck)
        {
            if (await ReconcileOneAsync(orderId, Origin, cancellationToken) is { Changed: true })
            {
                corrected++;
            }
        }

        return corrected;
    }

    public async Task<OrderStatus?> ReconcileOrderAsync(Guid orderId, string origin, CancellationToken cancellationToken = default) =>
        (await ReconcileOneAsync(orderId, origin, cancellationToken))?.Status;

    private async Task<(OrderStatus Status, bool Changed)?> ReconcileOneAsync(Guid orderId, string origin, CancellationToken cancellationToken)
    {
        var sessionId = await db.Orders
            .Where(o => o.Id == orderId && (o.Status == OrderStatus.AwaitingPayment || o.Status == OrderStatus.AwaitingConfirmation))
            .Select(o => o.StripeSessionId)
            .SingleOrDefaultAsync(cancellationToken);

        if (sessionId is null)
        {
            return null;
        }

        try
        {
            var session = await payments.GetCheckoutSessionAsync(sessionId, cancellationToken);

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var order = await db.Orders.SingleAsync(o => o.Id == orderId, cancellationToken);
            var before = order.Status;

            if (session.Status == "expired")
            {
                await transitions.ApplyExpiredAsync(origin, order, cancellationToken);
            }
            else if (session.PaymentStatus == "paid")
            {
                await transitions.ApplyPaymentAsync(origin, order, session, cancellationToken);
            }
            else
            {
                logger.LogInformation("{Origin}: sessão do pedido {OrderId} ainda está {SessionStatus}/{PaymentStatus}; nada a fazer", origin, order.Id, session.Status, session.PaymentStatus);
                return (order.Status, false);
            }

            await transaction.CommitAsync(cancellationToken);

            var changed = order.Status != before;
            if (changed)
            {
                logger.LogWarning("{Origin}: pedido {OrderId} passou de {Before} para {Status}; um evento do webhook se perdeu ou ainda não chegou", origin, order.Id, before, order.Status);
            }

            return (order.Status, changed);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();

            logger.LogInformation("{Origin}: pedido {OrderId} mudou durante a conferência", origin, orderId);
            var current = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync(cancellationToken);
            return (current, false);
        }
        catch (PaymentGatewayException ex)
        {
            logger.LogWarning(ex, "{Origin}: não foi possível consultar a sessão do pedido {OrderId}", origin, orderId);
            return null;
        }
    }
}
