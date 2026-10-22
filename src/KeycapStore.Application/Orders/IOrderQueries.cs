namespace KeycapStore.Application.Orders;

public interface IOrderQueries
{
    Task<OrderView?> GetForCustomerAsync(Guid orderId, string customerId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderSummaryView>> ListForCustomerAsync(string customerId, CancellationToken cancellationToken = default);
}
