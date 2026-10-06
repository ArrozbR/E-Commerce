using System.Security.Cryptography;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.Infrastructure.Identity;

public sealed record CreateAdminResult(bool Succeeded, string? GeneratedPassword, IReadOnlyList<string> Errors);

public static class AdminCommand
{
    public const string AdminRole = "Admin";

    public static async Task<CreateAdminResult> CreateAdminAsync(this IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        if (!await roles.RoleExistsAsync(AdminRole))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole(AdminRole));
            if (!roleResult.Succeeded)
            {
                return Failure(roleResult);
            }
        }

        string? generatedPassword = null;
        var user = await users.FindByEmailAsync(email);

        if (user is null)
        {
            generatedPassword = GeneratePassword();
            user = new IdentityUser { UserName = email, Email = email };

            var createResult = await users.CreateAsync(user, generatedPassword);
            if (!createResult.Succeeded)
            {
                return Failure(createResult);
            }
        }

        if (!await users.IsInRoleAsync(user, AdminRole))
        {
            var addResult = await users.AddToRoleAsync(user, AdminRole);
            if (!addResult.Succeeded)
            {
                return Failure(addResult);
            }
        }

        return new CreateAdminResult(true, generatedPassword, []);
    }

    private static CreateAdminResult Failure(IdentityResult result) =>
        new(false, null, [.. result.Errors.Select(e => e.Description).Distinct()]);

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%&*?-_";

        var password = new char[20];
        password[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
        password[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
        password[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
        password[3] = symbols[RandomNumberGenerator.GetInt32(symbols.Length)];

        const string all = upper + lower + digits + symbols;
        for (var i = 4; i < password.Length; i++)
        {
            password[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
        }

        RandomNumberGenerator.Shuffle(password.AsSpan());
        return new string(password);
    }
}
