namespace KeycapStore.Domain.Orders;

public sealed class OrderNotFoundException(Guid orderId)
    : Exception($"O pedido {orderId} não existe.")
{
    public Guid OrderId { get; } = orderId;
}
