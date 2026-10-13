using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class OrphanOrderReleaser(AppDbContext db, TimeProvider clock, ILogger<OrphanOrderReleaser> logger) : IOrphanOrderReleaser
{
    public static readonly TimeSpan OrphanAge = TimeSpan.FromMinutes(10);

    public async Task<int> ReleaseOrphansAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow() - OrphanAge;

        var orphanIds = await db.Orders
            .Where(o => o.Status == OrderStatus.AwaitingPayment && o.StripeSessionId == null && o.CreatedAt < cutoff)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var released = 0;
        foreach (var orderId in orphanIds)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

                var order = await db.Orders.SingleAsync(o => o.Id == orderId, cancellationToken);

                if (order.Status is not OrderStatus.AwaitingPayment || order.StripeSessionId is not null)
                {
                    continue;
                }

                order.MarkFailed();
                await db.ReleaseStockAsync(order, cancellationToken);

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                released++;
                logger.LogInformation("Pedido órfão {OrderId} (sem sessão da Stripe) marcado como falho e estoque devolvido", orderId);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();

                logger.LogInformation("Pedido órfão {OrderId} mudou durante a liberação; ignorado", orderId);
            }
        }

        return released;
    }
}
