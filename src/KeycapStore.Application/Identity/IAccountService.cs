namespace KeycapStore.Application.Identity;

public interface IAccountService
{
    Task<AccountResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<AccountResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);
}
