using System.Net;

using KeycapStore.Application.Orders;
using KeycapStore.Infrastructure;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class ShippingQuoteRateLimitTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private static async Task<(HttpClient Client, string Token)> CustomerWithACartAsync(WebApplicationFactory<Program> factory, string email)
    {
        var client = await PageClient.LoggedInAsync(factory, email);
        var add = await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = KitAurora,
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Catalog")),
        }));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);

        return (client, PageClient.TokenFrom(await client.GetStringAsync("/Checkout")));
    }

    private static Task<HttpResponseMessage> AskForShippingAsync(HttpClient client, string token) =>
        client.PostAsync("/Checkout/Shipping", new FormUrlEncodedContent(new Dictionary<string, string>
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

    [Fact]
    public async Task OneCustomer_PastTenQuotesInAMinute_IsRefused_WithoutCallingTheMelhorEnvio_AndOthersAreNotAffected()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = await postgres.CreateMigratedFactoryAsync(shipping);
        var (eager, eagerToken) = await CustomerWithACartAsync(factory, "cota-demais@example.com");
        var (calm, calmToken) = await CustomerWithACartAsync(factory, "cota-normal@example.com");

        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await AskForShippingAsync(eager, eagerToken)).StatusCode);
        }

        var refused = await AskForShippingAsync(eager, eagerToken);

        Assert.Equal(HttpStatusCode.TooManyRequests, refused.StatusCode);
        Assert.Contains("Espere um minuto", await refused.Content.ReadAsStringAsync());
        Assert.Equal(10, shipping.Requests.Count);
        Assert.Equal(HttpStatusCode.OK, (await AskForShippingAsync(calm, calmToken)).StatusCode);
    }

    [Fact]
    public async Task AllCustomersTogether_PastTheTotalLimit_AreRefused()
    {
        var shipping = new FakeShippingQuotes();
        await using var factory = postgres.CreateFactory().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ShippingQuoteLimits:TotalPerMinute", "3");
            builder.ConfigureTestServices(services => services.AddSingleton<IShippingQuotes>(shipping));
        });
        await factory.Services.MigrateDatabaseAsync();
        var (first, firstToken) = await CustomerWithACartAsync(factory, "total-um@example.com");
        var (second, secondToken) = await CustomerWithACartAsync(factory, "total-dois@example.com");

        Assert.Equal(HttpStatusCode.OK, (await AskForShippingAsync(first, firstToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AskForShippingAsync(second, secondToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AskForShippingAsync(first, firstToken)).StatusCode);

        Assert.Equal(HttpStatusCode.TooManyRequests, (await AskForShippingAsync(second, secondToken)).StatusCode);
        Assert.Equal(3, shipping.Requests.Count);
    }
}
