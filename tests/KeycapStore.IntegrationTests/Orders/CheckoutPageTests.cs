using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.Application.Identity;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class CheckoutPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Senha#Forte123";

    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private static string TokenFrom(string html) =>
        Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;

    private static async Task<HttpClient> LoggedInClientAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, Password);
        }

        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        var token = TokenFrom(await client.GetStringAsync("/Account/Login"));
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        return client;
    }

    private static async Task AddKitAuroraAsync(HttpClient client)
    {
        var token = TokenFrom(await client.GetStringAsync("/Catalog"));
        var add = await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = KitAurora,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostAddressAsync(HttpClient client, string postalCode)
    {
        var token = TokenFrom(await client.GetStringAsync("/Checkout"));
        return await client.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
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
    public async Task Checkout_WhenNotLoggedIn_RedirectsToLogin()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Checkout");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Checkout_WithValidAddress_CreatesTheOrder_AndEmptiesTheCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInClientAsync(factory, "checkout@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostAddressAsync(client, "80020-310");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Orders/Details/", response.Headers.Location?.OriginalString);
        Assert.Contains("Seu carrinho está vazio.", WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")));
    }

    [Fact]
    public async Task Checkout_WithInvalidPostalCode_ShowsTheError_AndKeepsTheCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInClientAsync(factory, "cep@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostAddressAsync(client, "123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("CEP inválido", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Contains("Kit Aurora (base)", WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")));
    }

    [Fact]
    public async Task Checkout_WithEmptyCart_ShowsTheError()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInClientAsync(factory, "vazio@example.com");

        var response = await PostAddressAsync(client, "80020-310");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Seu carrinho está vazio.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
}
