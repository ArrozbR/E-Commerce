using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KeycapStore.Infrastructure.Notifications;

internal sealed class EmailComposer(AppDbContext db, IOptions<PublicSiteOptions> store)
{
    public async Task<EmailMessage?> ComposeAsync(OutboxEmail email, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == email.OrderId, cancellationToken);

        if (order?.CustomerId is null)
        {
            return null;
        }

        var to = await db.Users
            .Where(u => u.Id == order.CustomerId)
            .Select(u => u.Email)
            .SingleOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(to))
        {
            return null;
        }

        var batchCancelled = email.Kind == EmailKind.OrderRefunded
            && order.PreOrderShipsAt is not null
            && await db.PreOrderOutcomeAsync(order, cancellationToken) == PreOrderOutcome.Cancelled;

        var link = new Uri(new Uri(store.Value.PublicUrl), $"Orders/Details/{order.Id}");

        return EmailTemplates.Compose(email.Kind, order, to, link, email.CreatedAt, batchCancelled);
    }
}
