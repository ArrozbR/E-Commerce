using System.Net;

using KeycapStore.Application.Carts;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Localization;

public class EnglishPagesTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task InEnglishAsync(HttpClient client)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        var response = await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "en-US",
            ["returnUrl"] = "/Catalog",
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> PageAsync(HttpClient client, string path) =>
        WebUtility.HtmlDecode(await client.GetStringAsync(path));

    [Fact]
    public async Task Catalog_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        await InEnglishAsync(client);

        var html = await PageAsync(client, "/Catalog");

        Assert.Contains("<h1 class=\"mb-4\">Catalog</h1>", html);
        Assert.Contains(">In stock<", html);
        Assert.Contains(">Sold out<", html);
        Assert.Contains(">Add to cart<", html);
    }

    [Fact]
    public async Task EmptyCart_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "carrinho-ingles@example.com");
        await InEnglishAsync(client);

        var html = await PageAsync(client, "/Cart");

        Assert.Contains("Your cart is empty.", html);
        Assert.Contains("Browse the catalog", html);
    }

    [Fact]
    public async Task LoginPage_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        await InEnglishAsync(client);

        var html = await PageAsync(client, "/Account/Login");

        Assert.Contains("<h1>Sign in</h1>", html);
    }

    [Fact]
    public async Task MyAccount_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "conta-ingles@example.com");
        await InEnglishAsync(client);

        var html = await PageAsync(client, "/Account/Delete");

        Assert.Contains("Signed in as conta-ingles@example.com", html);
        Assert.Contains("No saved address.", html);
        Assert.Contains("Delete my account", html);
    }

    [Fact]
    public async Task OrderPage_InEnglish_WithShippingInReais()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "pedido-ingles@example.com");
        Guid orderId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync("pedido-ingles@example.com");
            await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync(user!.Id, KitAurora, 1);
            var placed = await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceOrderAsync(user.Id, Curitiba);
            orderId = placed.OrderId!.Value;
        }
        await InEnglishAsync(client);

        var html = await PageAsync(client, $"/Orders/Details/{orderId}");

        Assert.Contains("Order placed", html);
        Assert.Contains(">Shipping<", html);
        Assert.Contains(">R$15.00<", html);
        Assert.Contains(">R$364.90<", html);
        Assert.Contains("Postal code 80020-310", html);
    }
}
