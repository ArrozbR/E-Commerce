using System.Net;

using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Localization;

public class EnglishCheckoutTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private static async Task ChooseAsync(HttpClient client, string culture)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        var response = await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = culture,
            ["returnUrl"] = "/Catalog",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> BuyAndGetTheOrderLanguageAsync(WebApplicationFactory<Program> factory, FakePaymentGateway stripe, HttpClient client)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = KitAurora,
            ["__RequestVerificationToken"] = token,
        }));

        token = PageClient.TokenFrom(await client.GetStringAsync("/Checkout"));
        var response = await client.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RecipientName"] = "Bruno Lima",
            ["PostalCode"] = "80020-310",
            ["Street"] = "Rua XV de Novembro",
            ["Number"] = "200",
            ["District"] = "Centro",
            ["City"] = "Curitiba",
            ["State"] = "PR",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var orderId = stripe.RequestedOrders.Single();
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders
            .Where(o => o.Id == orderId).Select(o => o.Language).SingleAsync();
    }

    [Fact]
    public async Task OrderPlacedInEnglish_RemembersTheLanguage()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "pedido-em-ingles@example.com");
        await ChooseAsync(client, "en-US");

        Assert.Equal("en-US", await BuyAndGetTheOrderLanguageAsync(factory, stripe, client));
    }

    [Fact]
    public async Task OrderPlacedInPortuguese_RemembersTheLanguage()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var client = await PageClient.LoggedInAsync(factory, "pedido-em-portugues@example.com");

        Assert.Equal("pt-BR", await BuyAndGetTheOrderLanguageAsync(factory, stripe, client));
    }
}
