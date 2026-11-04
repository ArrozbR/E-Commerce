using KeycapStore.Application.Orders;
using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.Infrastructure.Orders;

internal sealed class CancellationService(AppDbContext db, IPaymentGateway payments) : ICancellationService
{
    public async Task<CancelPaymentResult> CancelPaymentAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default)
    {
        var order = await db.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

        if (order.Status is not OrderStatus.AwaitingPayment || order.StripeSessionId is null)
        {
            return CancelPaymentResult.NotAwaitingPayment;
        }

        try
        {
            await payments.ExpireCheckoutSessionAsync(order.StripeSessionId, cancellationToken);
        }
        catch (PaymentGatewayException)
        {
            return CancelPaymentResult.PaymentServiceUnavailable;
        }

        return CancelPaymentResult.Requested;
    }

    public async Task CancelAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId && o.CustomerId == customerId, cancellationToken)
            ?? throw new OrderNotFoundException(orderId);

        order.Cancel();
        await db.ReleaseStockAsync(order, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
