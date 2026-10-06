using KeycapStore.Application.Identity;

using Microsoft.AspNetCore.Identity;

namespace KeycapStore.Infrastructure.Identity;

internal sealed class AccountService(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager) : IAccountService
{
    public async Task<AccountResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = new IdentityUser { UserName = email, Email = email };

        var result = await userManager.CreateAsync(user, password);

        return result.Succeeded
            ? AccountResult.Success()
            : AccountResult.Failure(result.Errors.Select(e => e.Description).Distinct());
    }


    public async Task<AccountResult> SignInAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var result = await signInManager.PasswordSignInAsync(email, password, isPersistent: false, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return AccountResult.Success();
        }

        return result.IsLockedOut
            ? AccountResult.Failure(["Muitas tentativas erradas. Tente de novo em alguns minutos."])
            : AccountResult.Failure(["E-mail ou senha inválidos."]);
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) => signInManager.SignOutAsync();
}
