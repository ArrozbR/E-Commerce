using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class PreOrderCloser(AppDbContext db, TimeProvider clock, ILogger<PreOrderCloser> logger) : IPreOrderCloser
{
    public static readonly TimeSpan ClosingDelay = TimeSpan.FromHours(1);

    public async Task<int> CloseDueCampaignsAsync(CancellationToken cancellationToken = default)
    {
        var limit = clock.GetUtcNow() - ClosingDelay;
        var due = await db.Products.AsNoTracking()
            .Where(p => p.PreOrderEndsAt != null && p.PreOrderOutcome == null && p.PreOrderEndsAt <= limit)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var closed = 0;
        foreach (var productId in due)
        {
            if (await CloseAsync(productId, cancellationToken))
            {
                closed++;
            }
        }

        return closed;
    }

    private async Task<bool> CloseAsync(Guid productId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var limit = clock.GetUtcNow() - ClosingDelay;
        var locked = await db.Products
            .Where(p => p.Id == productId && p.PreOrderOutcome == null && p.PreOrderEndsAt <= limit)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false), cancellationToken);
        if (locked == 0)
        {
            return false;
        }

        var pending = await db.Orders.AnyAsync(
            o => (o.Status == OrderStatus.AwaitingPayment || o.Status == OrderStatus.AwaitingConfirmation)
                && o.Items.Any(i => i.ProductId == productId),
            cancellationToken);
        if (pending)
        {
            logger.LogInformation("Pré-venda {ProductId}: ainda há pagamento pendente; o fechamento fica para a próxima rodada", productId);
            return false;
        }

        var product = await db.Products.SingleAsync(p => p.Id == productId, cancellationToken);

        var sold = (await db.SoldUnitsAsync([productId], cancellationToken)).GetValueOrDefault(productId);
        var minimumReached = sold >= product.PreOrder!.MinimumUnits;

        if (minimumReached)
        {
            product.ConfirmPreOrder();
        }
        else
        {
            product.CancelPreOrder();

            var paidOrders = await db.Orders
                .Where(o => o.Status == OrderStatus.Paid && o.Items.Any(i => i.ProductId == productId))
                .ToListAsync(cancellationToken);

            foreach (var order in paidOrders)
            {
                order.Cancel();
                await db.ReleaseStockAsync(order, cancellationToken);
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            logger.LogWarning("Pré-venda {ProductId}: um pedido mudou durante o fechamento; nova tentativa na próxima rodada", productId);
            return false;
        }

        await transaction.CommitAsync(cancellationToken);

        if (minimumReached)
        {
            logger.LogInformation("Pré-venda {ProductId} confirmada com {Sold} unidades vendidas", productId, sold);
        }
        else
        {
            logger.LogWarning("Pré-venda {ProductId} cancelada com {Sold} unidades vendidas; pedidos cancelados para reembolso", productId, sold);
        }

        return true;
    }
}
