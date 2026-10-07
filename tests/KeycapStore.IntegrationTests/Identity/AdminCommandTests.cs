using KeycapStore.Application.Identity;
using KeycapStore.Infrastructure;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class AdminCommandTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task CreateAdmin_WithNewEmail_CreatesAdminWithWorkingPassword()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var result = await factory.Services.CreateAdminAsync("admin@example.com");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.NotNull(result.GeneratedPassword);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = await users.FindByEmailAsync("admin@example.com");
        Assert.NotNull(admin);
        Assert.True(await users.IsInRoleAsync(admin, AdminCommand.AdminRole));
        Assert.True(await users.CheckPasswordAsync(admin, result.GeneratedPassword));
    }

    [Fact]
    public async Task CreateAdmin_WithExistingAccount_PromotesAndKeepsPassword()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using (var registerScope = factory.Services.CreateAsyncScope())
        {
            var accounts = registerScope.ServiceProvider.GetRequiredService<IAccountService>();
            await accounts.RegisterAsync("cliente@example.com", "Senha#Forte123");
        }

        var result = await factory.Services.CreateAdminAsync("cliente@example.com");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Null(result.GeneratedPassword);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync("cliente@example.com");
        Assert.NotNull(user);
        Assert.True(await users.IsInRoleAsync(user, AdminCommand.AdminRole));
        Assert.True(await users.CheckPasswordAsync(user, "Senha#Forte123"));
    }

    [Fact]
    public async Task CreateAdmin_RunTwice_DoesNotDuplicate()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var first = await factory.Services.CreateAdminAsync("duas-vezes@example.com");
        var second = await factory.Services.CreateAdminAsync("duas-vezes@example.com");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Null(second.GeneratedPassword);

        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admins = await users.GetUsersInRoleAsync(AdminCommand.AdminRole);
        Assert.Single(admins, u => u.Email == "duas-vezes@example.com");
    }
}
