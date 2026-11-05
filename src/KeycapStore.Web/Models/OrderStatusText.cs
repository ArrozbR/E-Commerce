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

    public static string Tone(OrderStatus status) => status switch
    {
        OrderStatus.Paid or OrderStatus.Picking or OrderStatus.Shipped or OrderStatus.Delivered => "success",
        OrderStatus.AwaitingPayment or OrderStatus.AwaitingConfirmation or OrderStatus.PaidOutOfStock
            or OrderStatus.Cancelled or OrderStatus.Withdrawn or OrderStatus.AwaitingReturn => "warning",
        OrderStatus.Failed => "danger",
        _ => "neutral",
    };

    public static string BadgeClass(OrderStatus status) => Tone(status) switch
    {
        "success" => "badge badge-success",
        "warning" => "badge badge-warning",
        "danger" => "badge badge-danger",
        _ => "badge badge-neutral",
    };

    public static int? Step(OrderStatus status) => status switch
    {
        OrderStatus.AwaitingPayment or OrderStatus.AwaitingConfirmation => 1,
        OrderStatus.Paid => 2,
        OrderStatus.Picking => 3,
        OrderStatus.Shipped => 4,
        OrderStatus.Delivered => 5,
        _ => null,
    };

    public static string? Explanation(OrderStatus status) => status switch
    {
        OrderStatus.Expired => "O tempo para pagar acabou e os itens voltaram para a loja. Nada foi cobrado.",
        OrderStatus.Failed => "O pagamento não foi aprovado e os itens voltaram para a loja.",
        OrderStatus.PaidOutOfStock => "O pagamento chegou depois que a reserva acabou e o kit já tinha sido vendido. O valor é devolvido automaticamente.",
        OrderStatus.Refunded => "O valor pago foi devolvido.",
        OrderStatus.Cancelled => "Pedido cancelado. O reembolso é feito automaticamente.",
        OrderStatus.Withdrawn => "Desistência registrada. O reembolso é feito automaticamente; depois, devolva o produto à loja.",
        OrderStatus.AwaitingReturn => "O valor já foi devolvido. Agora falta devolver o produto à loja.",
        OrderStatus.Returned => "A loja recebeu o produto de volta. Pedido encerrado.",
        _ => null,
    };
}
