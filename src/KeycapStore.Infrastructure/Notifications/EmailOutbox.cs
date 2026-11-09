using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace KeycapStore.Infrastructure.Notifications;

internal static class EmailOutbox
{
    public const string KindPerOrderIndex = "IX_outbox_emails_Kind_OrderId";

    public static void QueueEmail(this AppDbContext db, EmailKind kind, Guid orderId, DateTimeOffset now) =>
        db.OutboxEmails.Add(new OutboxEmail(kind, orderId, now));

    public static bool IsAlreadyQueued(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: KindPerOrderIndex };
}
