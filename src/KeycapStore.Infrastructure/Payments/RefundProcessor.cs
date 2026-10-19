using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class RefundProcessor(AppDbContext db, IPaymentGateway payments, ILogger<RefundProcessor> logger) : IRefundProcessor
{
    public async Task<int> RefundPendingAsync(CancellationToken cancellationToken = default)
    {
        var pendingIds = await db.Orders
            .Where(o => o.Status == OrderStatus.PaidOutOfStock || o.Status == OrderStatus.Cancelled || o.Status == OrderStatus.Withdrawn)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var refunded = 0;
        foreach (var orderId in pendingIds)
        {
            try
            {
                var order = await db.Orders.SingleAsync(o => o.Id == orderId, cancellationToken);

                if (order.Status is not (OrderStatus.PaidOutOfStock or OrderStatus.Cancelled or OrderStatus.Withdrawn))
                {
                    continue;
                }

                var paymentIntentId = order.StripePaymentIntentId ?? await payments.GetPaymentIntentIdAsync(order.StripeSessionId!, cancellationToken);

                await payments.RefundAsync(order.Id, paymentIntentId, cancellationToken);

                order.MarkRefunded();
                await db.SaveChangesAsync(cancellationToken);

                refunded++;
                logger.LogInformation("Pedido {OrderId} reembolsado", orderId);
            }
            catch (PaymentGatewayException)
            {
                logger.LogError("Reembolso do pedido {OrderId} falhou; ele continua aguardando reembolso e será tentado de novo", orderId);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                logger.LogInformation("Pedido {OrderId} mudou durante o reembolso; ignorado", orderId);
            }
        }

        return refunded;
    }
}
