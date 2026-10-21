namespace KeycapStore.Application.Identity;

public interface IAccountDeletionService
{
    Task<AccountDeletionResult> DeleteAsync(string customerId, string password, CancellationToken cancellationToken = default);
}
