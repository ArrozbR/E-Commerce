using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Orders;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.Extensions.Logging;

using Stripe.Checkout;

namespace KeycapStore.Infrastructure.Payments;

internal sealed class CheckoutSessionTransitions(AppDbContext db, ILogger<CheckoutSessionTransitions> logger)
{
    public static CheckoutSessionState StateOf(Session session) =>
        new(session.Status, session.PaymentStatus, session.AmountTotal, session.Currency, session.PaymentIntentId, session.ClientSecret);

    public async Task ApplyPaymentAsync(string origin, Order order, CheckoutSessionState session, CancellationToken cancellationToken)
    {
        if (session.AmountTotal != StripeCheckoutSessionFactory.ToCents(order.Total) || session.Currency != "brl")
        {
            logger.LogError("{Origin}: valor pago diferente do total do pedido {OrderId}; pedido NÃO confirmado", origin, order.Id);
            return;
        }

        if (session.PaymentStatus == "paid")
        {
            await ApplyPaidAsync(origin, order, session, cancellationToken);
        }
        else if (session.PaymentStatus == "unpaid" && order.Status is OrderStatus.AwaitingPayment)
        {
            order.MarkAwaitingConfirmation();
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("{Origin}: pedido {OrderId} agora está {Status}", origin, order.Id, order.Status);
    }

    public async Task ApplyExpiredAsync(string origin, Order order, CancellationToken cancellationToken)
    {
        if (order.Status is not OrderStatus.AwaitingPayment)
        {
            logger.LogInformation("{Origin}: pedido {OrderId} já está {Status}; expiração ignorada", origin, order.Id, order.Status);
            return;
        }

        order.Expire();
        await db.ReleaseStockAsync(order, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("{Origin}: pedido {OrderId} expirado e estoque devolvido", origin, order.Id);
    }

    private async Task ApplyPaidAsync(string origin, Order order, CheckoutSessionState session, CancellationToken cancellationToken)
    {
        if (order.Status is OrderStatus.AwaitingPayment or OrderStatus.AwaitingConfirmation or OrderStatus.Paid)
        {
            order.ConfirmPayment(session.PaymentIntentId);
        }
        else if (order.Status is OrderStatus.Expired or OrderStatus.Failed)
        {
            if (await db.TryReserveStockAsync(order, cancellationToken))
            {
                order.ConfirmLatePayment(session.PaymentIntentId);
            }
            else
            {
                order.MarkOutOfStock(session.PaymentIntentId!);

                logger.LogError("{Origin}: pedido {OrderId} foi pago depois de liberar a reserva e não há estoque; reembolso necessário", origin, order.Id);
            }
        }
        else
        {
            logger.LogInformation("{Origin}: pedido {OrderId} já está {Status}; pagamento repetido ignorado", origin, order.Id, order.Status);
        }
    }
}
