namespace KeycapStore.Domain.Orders;

public sealed class InvalidOrderTransitionException(OrderStatus from, OrderStatus to)
    : Exception($"O pedido não pode ir de {from} para {to}.")
{
    public OrderStatus From { get; } = from;
    public OrderStatus To { get; } = to;
}
