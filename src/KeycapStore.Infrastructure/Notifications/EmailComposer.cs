using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Notifications;

internal sealed class EmailComposer(AppDbContext db)
{
    public async Task<EmailMessage?> ComposeAsync(OutboxEmail email, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Where(o => o.Id == email.OrderId)
            .Select(o => new { o.Id, o.CustomerId })
            .SingleOrDefaultAsync(cancellationToken);

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

        var number = order.Id.ToString()[..8];
        var subject = email.Kind switch
        {
            EmailKind.OrderPaid => $"Pagamento confirmado: pedido {number}",
            EmailKind.OrderShipped => $"Pedido {number} enviado",
            EmailKind.OrderRefunded => $"Reembolso do pedido {number}",
            EmailKind.WithdrawalReceived => $"Desistência do pedido {number} recebida",
            _ => throw new ArgumentOutOfRangeException(nameof(email), email.Kind, "Tipo de e-mail desconhecido."),
        };

        return new EmailMessage(to, subject, subject, $"<p>{subject}</p>");
    }
}
