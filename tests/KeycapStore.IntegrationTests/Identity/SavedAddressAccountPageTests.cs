using System.Net;

using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class SavedAddressAccountPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Paulista =
        new("Ana Souza", "Avenida Paulista", "1000", "Apto 12", "Bela Vista", "São Paulo", "SP", "01310100");

    private static async Task<string> CustomerIdAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email);
        return user!.Id;
    }

    private static async Task SavePaulistaAsync(WebApplicationFactory<Program> factory, string email)
    {
        var customerId = await CustomerIdAsync(factory, email);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync(customerId, Paulista);
    }

    [Fact]
    public async Task MyAccount_WithoutSavedAddress_SaysSo()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "sem-endereco@example.com");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete"));

        Assert.Contains("Nenhum endereço salvo.", html);
    }

    [Fact]
    public async Task MyAccount_ShowsTheSavedAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "com-endereco@example.com");
        await SavePaulistaAsync(factory, "com-endereco@example.com");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete"));

        Assert.Contains("Avenida Paulista, 1000 - Apto 12", html);
        Assert.Contains("CEP 01310-100", html);
        Assert.Contains("Remover endereço salvo", html);
    }

    [Fact]
    public async Task RemoveButton_DeletesTheSavedAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "remove-endereco@example.com");
        await SavePaulistaAsync(factory, "remove-endereco@example.com");

        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Delete"));
        var response = await client.PostAsync("/Account/RemoveSavedAddress", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete"));
        Assert.Contains("Endereço salvo removido.", html);
        Assert.Contains("Nenhum endereço salvo.", html);
    }
}
