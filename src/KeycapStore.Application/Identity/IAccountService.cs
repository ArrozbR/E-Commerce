namespace KeycapStore.Application.Identity;

public interface IAccountService
{
    Task<AccountResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AccountResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<ExternalLoginResult> SignInWithExternalLoginAsync(CancellationToken cancellationToken = default);

    Task<LinkExternalLoginResult> LinkExternalLoginAsync(string customerId, CancellationToken cancellationToken = default);

    Task<AccountSummary> GetSummaryAsync(string customerId, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
