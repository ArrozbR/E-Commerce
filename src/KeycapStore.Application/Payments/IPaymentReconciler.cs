using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Payments;

public interface IPaymentReconciler
{
    Task<int> ReconcileAsync(CancellationToken cancellationToken = default);

    Task<OrderStatus?> ReconcileOrderAsync(Guid orderId, string origin, CancellationToken cancellationToken = default);
}
