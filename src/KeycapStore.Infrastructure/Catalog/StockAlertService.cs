using KeycapStore.Application.Catalog;
using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Notifications;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Npgsql;

namespace KeycapStore.Infrastructure.Catalog;

internal sealed class StockAlertService(AppDbContext db, TimeProvider clock) : IStockAlertService
{
    public async Task<StockAlertResult> SubscribeAsync(string customerId, Guid productId, string language, CancellationToken cancellationToken = default)
    {
        var product = await db.Products
            .AsNoTracking()
            .Where(p => p.Id == productId)
            .Select(p => new { p.IsActive, p.Stock, p.PreOrderEndsAt })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new ProductNotFoundException(productId);

        if (!product.IsActive || product.PreOrderEndsAt is not null || product.Stock > 0)
        {
            return StockAlertResult.NotAllowed;
        }

        db.StockAlerts.Add(new StockAlert(customerId, productId, language, clock.GetUtcNow()));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return StockAlertResult.Subscribed;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return StockAlertResult.AlreadySubscribed;
        }
    }

    public Task UnsubscribeAsync(string customerId, Guid productId, CancellationToken cancellationToken = default) =>
        db.StockAlerts
            .Where(a => a.CustomerId == customerId && a.ProductId == productId)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<bool> IsSubscribedAsync(string customerId, Guid productId, CancellationToken cancellationToken = default) =>
        db.StockAlerts.AnyAsync(a => a.CustomerId == customerId && a.ProductId == productId, cancellationToken);
}

internal sealed class StockAlertNotifier(AppDbContext db, TimeProvider clock, ILogger<StockAlertNotifier> logger) : IStockAlertNotifier
{
    public const int BatchSize = 200;

    public async Task<int> NotifyAsync(CancellationToken cancellationToken = default)
    {
        var due = await db.StockAlerts
            .Where(a => db.Products.Any(p => p.Id == a.ProductId && p.IsActive && p.PreOrderEndsAt == null && p.Stock > 0))
            .OrderBy(a => a.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (due.Count == 0)
        {
            return 0;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        foreach (var alert in due)
        {
            db.OutboxEmails.Add(OutboxEmail.AboutProduct(EmailKind.BackInStock, alert.CustomerId, alert.ProductId, alert.Language, now));
            db.StockAlerts.Remove(alert);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            logger.LogInformation("Avisos de estoque mudaram durante a rodada; nenhum foi anotado, a próxima rodada confere de novo");
            return 0;
        }

        logger.LogInformation("{Count} avisos de volta ao estoque anotados", due.Count);
        return due.Count;
    }
}
