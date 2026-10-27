namespace KeycapStore.Application.Payments;

public interface IPaymentReconciler
{
    Task<int> ReconcileAsync(CancellationToken cancellationToken = default);
}
