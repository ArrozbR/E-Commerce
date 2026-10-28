using System.Security.Claims;

using KeycapStore.Application.Identity;

using Microsoft.AspNetCore.Authentication;
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

    public async Task<ExternalLoginResult> SignInWithExternalLoginAsync(CancellationToken cancellationToken = default)
    {
        var info = await signInManager.GetExternalLoginInfoAsync();
        await signInManager.Context.SignOutAsync(IdentityConstants.ExternalScheme);
        if (info is null)
        {
            return ExternalLoginResult.Failed;
        }

        var user = await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
        if (user is null)
        {
            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrWhiteSpace(email))
            {
                return ExternalLoginResult.Failed;
            }

            if (await userManager.FindByEmailAsync(email) is not null)
            {
                return ExternalLoginResult.EmailAlreadyRegistered;
            }

            user = new IdentityUser { UserName = email, Email = email };
            if (!(await userManager.CreateAsync(user)).Succeeded)
            {
                return ExternalLoginResult.Failed;
            }

            if (!(await userManager.AddLoginAsync(user, info)).Succeeded)
            {
                await userManager.DeleteAsync(user);
                return ExternalLoginResult.Failed;
            }
        }

        if (await userManager.IsInRoleAsync(user, Roles.Admin))
        {
            return ExternalLoginResult.AdminNotAllowed;
        }

        await signInManager.SignInAsync(user, isPersistent: false);
        return ExternalLoginResult.SignedIn;
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default) => signInManager.SignOutAsync();
}
