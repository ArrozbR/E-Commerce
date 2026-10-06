using KeycapStore.Application.Identity;
using KeycapStore.Infrastructure;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class AccountServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Senha#Forte123";

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", postgres.Container.GetConnectionString()));

        await factory.Services.MigrateDatabaseAsync();
        return factory;
    }

    [Fact]
    public async Task Register_WithValidData_CreatesUser()
    {
        await using var factory = await CreateFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();

        var result = await accounts.RegisterAsync("joao@example.com", Password);

        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.NotNull(await userManager.FindByEmailAsync("joao@example.com"));
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Fails()
    {
        await using var factory = await CreateFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
        await accounts.RegisterAsync("ana@example.com", Password);

        var result = await accounts.RegisterAsync("ana@example.com", Password);

        Assert.False(result.Succeeded);

        var error = Assert.Single(result.Errors);

        Assert.Equal("Este e-mail já está cadastrado.", error);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsErrorsInPortuguese()
    {
        await using var factory = await CreateFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();

        var result = await accounts.RegisterAsync("fraca@example.com", "123456");

        Assert.False(result.Succeeded);
        Assert.Contains("A senha precisa ter pelo menos uma letra maiúscula (A-Z).", result.Errors);
    }
}
