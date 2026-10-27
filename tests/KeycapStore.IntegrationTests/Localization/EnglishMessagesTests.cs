using System.Net;

using KeycapStore.Application.Carts;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Localization;

public class EnglishMessagesTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<HttpClient> LoggedInEnglishAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = await PageClient.LoggedInAsync(factory, email);
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        var response = await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "en-US",
            ["returnUrl"] = "/Catalog",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return client;
    }

    private static async Task<HttpResponseMessage> PostAddressAsync(HttpClient client, string postalCode)
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

    private static async Task<Guid> PlaceOrderAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email);
        await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync(user!.Id, KitAurora, 1);
        var placed = await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceOrderAsync(user.Id, Curitiba);
        return placed.OrderId!.Value;
    }

    [Fact]
    public async Task FormLabels_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInEnglishAsync(factory, "rotulos-ingles@example.com");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Checkout"));

        Assert.Contains("Recipient name", html);
        Assert.Contains("Postal code", html);
        Assert.Contains("Save this address to my account for future orders", html);
    }

    [Fact]
    public async Task ValidationMessage_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInEnglishAsync(factory, "validacao-ingles@example.com");

        var response = await PostAddressAsync(client, "123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Invalid postal code. Use 8 digits, like 01310-100.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task ResultMessage_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInEnglishAsync(factory, "mensagem-ingles@example.com");

        await PostAddressAsync(client, "80020-310");

        Assert.Contains("Address saved.", WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete")));
    }

    [Fact]
    public async Task OrderStatus_InEnglish_OnThePages()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInEnglishAsync(factory, "situacao-ingles@example.com");
        var orderId = await PlaceOrderAsync(factory, "situacao-ingles@example.com");

        Assert.Contains(">Awaiting payment<", WebUtility.HtmlDecode(await client.GetStringAsync("/orders")));
        Assert.Contains(">Awaiting payment<", WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{orderId}")));
    }

    [Fact]
    public async Task OrderStatus_InEnglish_OnTheReturnPageCheck()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await LoggedInEnglishAsync(factory, "consulta-ingles@example.com");
        var orderId = await PlaceOrderAsync(factory, "consulta-ingles@example.com");

        var json = await client.GetStringAsync($"/orders/{orderId}/status");

        Assert.Contains("\"text\":\"Awaiting payment\"", json);
    }

    [Fact]
    public async Task OrderStatus_StaysInPortuguese_ByDefault()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "situacao-portugues@example.com");
        var orderId = await PlaceOrderAsync(factory, "situacao-portugues@example.com");

        var json = await client.GetStringAsync($"/orders/{orderId}/status");

        Assert.Contains("\"text\":\"Aguardando pagamento\"", json);
    }
}
