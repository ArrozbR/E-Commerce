namespace KeycapStore.Application.Orders;

public interface IOrderQueries
{
    Task<OrderView?> GetForCustomerAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);
}
