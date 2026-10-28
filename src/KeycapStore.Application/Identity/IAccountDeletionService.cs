namespace KeycapStore.Application.Identity;

public interface IAccountDeletionService
{
    Task<AccountDeletionResult> DeleteAsync(string customerId, string confirmation, CancellationToken cancellationToken = default);
}
