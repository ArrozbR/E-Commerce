using KeycapStore.Application.Notifications;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Notifications;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class WithdrawalService(AppDbContext db, TimeProvider clock) : IWithdrawalService
{
    public async Task WithdrawAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

        var now = clock.GetUtcNow();
        order.Withdraw(now);
        db.QueueEmail(EmailKind.WithdrawalReceived, order.Id, now);

        await db.SaveChangesAsync(cancellationToken);
    }
}
