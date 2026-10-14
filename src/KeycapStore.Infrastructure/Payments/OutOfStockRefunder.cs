using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class OutOfStockRefunder(AppDbContext db, IPaymentGateway payments, ILogger<OutOfStockRefunder> logger) : IOutOfStockRefunder
{
    public async Task<int> RefundPendingAsync(CancellationToken cancellationToken = default)
    {
        var pendingIds = await db.Orders
            .Where(o => o.Status == OrderStatus.PaidOutOfStock)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var refunded = 0;
        foreach (var orderId in pendingIds)
        {
            try
            {
                var order = await db.Orders.SingleAsync(o => o.Id == orderId, cancellationToken);

                if (order.Status is not OrderStatus.PaidOutOfStock)
                {
                    continue;
                }

                await payments.RefundAsync(order.Id, order.StripePaymentIntentId!, cancellationToken);

                order.MarkRefunded();
                await db.SaveChangesAsync(cancellationToken);

                refunded++;
                logger.LogInformation("Pedido {OrderId} reembolsado (pago sem estoque)", orderId);
            }
            catch (PaymentGatewayException)
            {
                logger.LogError("Reembolso do pedido {OrderId} falhou; o pedido continua PaidOutOfStock e será tentado de novo", orderId);
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
