using KeycapStore.Application.Identity;

using Microsoft.AspNetCore.Identity;

namespace KeycapStore.Infrastructure.Identity;

internal sealed class AccountService(UserManager<IdentityUser> userManager) : IAccountService
{
    public async Task<AccountResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = new IdentityUser { UserName = email, Email = email };

        var result = await userManager.CreateAsync(user, password);

        return result.Succeeded
            ? AccountResult.Success()
            : AccountResult.Failure(result.Errors.Select(e => e.Description).Distinct());
    }
}
