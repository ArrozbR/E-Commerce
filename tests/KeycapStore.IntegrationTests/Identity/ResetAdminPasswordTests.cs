using KeycapStore.Application.Identity;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class ResetAdminPasswordTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Reset_ForAnAdmin_ReplacesThePassword_AndEndsOpenSessions()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var created = await factory.Services.CreateAdminAsync("admin-esquecido@example.com");
        string stampBefore;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            stampBefore = (await users.FindByEmailAsync("admin-esquecido@example.com"))!.SecurityStamp!;
        }

        var result = await factory.Services.ResetAdminPasswordAsync("admin-esquecido@example.com");

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.NotEqual(created.GeneratedPassword, result.NewPassword);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var admin = (await users.FindByEmailAsync("admin-esquecido@example.com"))!;
            Assert.False(await users.CheckPasswordAsync(admin, created.GeneratedPassword!));
            Assert.True(await users.CheckPasswordAsync(admin, result.NewPassword!));
            Assert.NotEqual(stampBefore, admin.SecurityStamp);
        }
    }

    [Fact]
    public async Task Reset_ForACustomer_IsRefused_AndKeepsThePassword()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync("cliente-reset@example.com", "Senha#Forte123");
        }

        var result = await factory.Services.ResetAdminPasswordAsync("cliente-reset@example.com");

        Assert.False(result.Succeeded);
        Assert.Null(result.NewPassword);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var customer = (await users.FindByEmailAsync("cliente-reset@example.com"))!;
            Assert.True(await users.CheckPasswordAsync(customer, "Senha#Forte123"));
        }
    }

    [Fact]
    public async Task Reset_ForAnUnknownEmail_IsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var result = await factory.Services.ResetAdminPasswordAsync("ninguem@example.com");

        Assert.False(result.Succeeded);
        Assert.Null(result.NewPassword);
    }
}
