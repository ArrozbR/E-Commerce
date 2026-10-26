using System.Net;

using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class AccountAddressPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Paulista =
        new("Ana Souza", "Avenida Paulista", "1000", "Apto 12", "Bela Vista", "São Paulo", "SP", "01310100");

    private static async Task<string> CustomerIdAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email);
        return user!.Id;
    }

    private static async Task<ShippingAddress?> SavedAddressOfAsync(WebApplicationFactory<Program> factory, string email)
    {
        var customerId = await CustomerIdAsync(factory, email);
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().GetAsync(customerId);
    }

    private static async Task<HttpResponseMessage> PostCuritibaAsync(HttpClient client, string postalCode)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Address"));
        return await client.PostAsync("/Account/Address", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RecipientName"] = "Bruno Lima",
            ["PostalCode"] = postalCode,
            ["Street"] = "Rua XV de Novembro",
            ["Number"] = "200",
            ["District"] = "Centro",
            ["City"] = "Curitiba",
            ["State"] = "PR",
            ["__RequestVerificationToken"] = token,
        }));
    }

    [Fact]
    public async Task AddressPage_WhenNotLoggedIn_RedirectsToLogin()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync("/Account/Address");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task MyAccount_WithoutSavedAddress_OffersToRegisterOne()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "oferece-cadastro@example.com");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete"));

        Assert.Contains("Cadastrar endereço", html);
    }

    [Fact]
    public async Task SavingFromMyAccount_CreatesTheAddress_AndTheCheckoutComesFilledIn()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "cadastra-pela-conta@example.com");

        var response = await PostCuritibaAsync(client, "80020-310");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Delete", response.Headers.Location?.OriginalString);
        Assert.Contains("Endereço salvo.", WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete")));

        var checkout = WebUtility.HtmlDecode(await client.GetStringAsync("/Checkout"));
        Assert.Contains("value=\"Rua XV de Novembro\"", checkout);
        Assert.Contains("value=\"80020310\"", checkout);
    }

    [Fact]
    public async Task SavingFromMyAccount_ReplacesTheOldAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "altera-pela-conta@example.com");
        var customerId = await CustomerIdAsync(factory, "altera-pela-conta@example.com");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync(customerId, Paulista);
        }

        var form = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Address"));
        Assert.Contains("value=\"Avenida Paulista\"", form);

        var response = await PostCuritibaAsync(client, "80020-310");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await SavedAddressOfAsync(factory, "altera-pela-conta@example.com");
        Assert.NotNull(saved);
        Assert.Equal("Rua XV de Novembro", saved.Street);
    }

    [Fact]
    public async Task SavingFromMyAccount_WithInvalidPostalCode_ShowsTheError_AndSavesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "cep-errado-na-conta@example.com");

        var response = await PostCuritibaAsync(client, "123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("CEP inválido", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Null(await SavedAddressOfAsync(factory, "cep-errado-na-conta@example.com"));
    }
}
