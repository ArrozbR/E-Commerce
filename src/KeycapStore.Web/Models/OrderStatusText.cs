using KeycapStore.Domain.Orders;

namespace KeycapStore.Web.Models;

public static class OrderStatusText
{
    public static string For(OrderStatus status) => status switch
    {
        OrderStatus.AwaitingPayment => "Aguardando pagamento",
        OrderStatus.AwaitingConfirmation => "Aguardando confirmação do pagamento",
        OrderStatus.Paid => "Pago",
        OrderStatus.Expired => "Expirado",
        OrderStatus.Failed => "Pagamento não aprovado",
        OrderStatus.PaidOutOfStock => "Pago, mas sem estoque",
        OrderStatus.Refunded => "Reembolsado",
        OrderStatus.Picking => "Em separação",
        OrderStatus.Shipped => "Enviado",
        OrderStatus.Delivered => "Entregue",
        OrderStatus.Cancelled => "Cancelado (reembolso em andamento)",
        OrderStatus.Withdrawn => "Desistência registrada (reembolso em andamento)",
        OrderStatus.AwaitingReturn => "Reembolsado, aguardando a devolução do produto",
        OrderStatus.Returned => "Devolvido",
        _ => status.ToString(),
    };
}
