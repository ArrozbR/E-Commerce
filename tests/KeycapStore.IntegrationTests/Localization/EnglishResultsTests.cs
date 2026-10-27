using System.Net;

using KeycapStore.Application.Carts;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Localization;

public class EnglishResultsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
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

    private static async Task<string> PlaceOrderPageAsync(HttpClient client)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Checkout"));
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static async Task CartWithAsync(WebApplicationFactory<Program> factory, string email, string productName, int stock, int quantity, bool active = true)
    {
        var product = new Product(productName, 100m, stock);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email);
        await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync(user!.Id, product.Id, quantity);

        if (!active)
        {
            await db.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));
        }
    }

    [Fact]
    public async Task EmptyCartAtCheckout_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "vazio-ingles@example.com");
        await InEnglishAsync(client);

        Assert.Contains("Your cart is empty.", await PlaceOrderPageAsync(client));
    }

    [Fact]
    public async Task NotEnoughStock_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "sem-estoque-ingles@example.com");
        await CartWithAsync(factory, "sem-estoque-ingles@example.com", "Kit Escasso Ingles", stock: 1, quantity: 2);
        await InEnglishAsync(client);

        Assert.Contains("Kit Escasso Ingles does not have enough stock.", await PlaceOrderPageAsync(client));
    }

    [Fact]
    public async Task NotEnoughStock_InPortuguese_ByDefault()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "sem-estoque-portugues@example.com");
        await CartWithAsync(factory, "sem-estoque-portugues@example.com", "Kit Escasso Portugues", stock: 1, quantity: 2);

        Assert.Contains("Kit Escasso Portugues não tem estoque suficiente.", await PlaceOrderPageAsync(client));
    }

    [Fact]
    public async Task ProductTakenOffSale_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "inativo-ingles@example.com");
        await CartWithAsync(factory, "inativo-ingles@example.com", "Kit Inativo Ingles", stock: 5, quantity: 1, active: false);
        await InEnglishAsync(client);

        Assert.Contains("Kit Inativo Ingles is no longer for sale. Remove it from your cart to continue.", await PlaceOrderPageAsync(client));
    }

    [Fact]
    public async Task WrongPassword_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await PageClient.LoggedInAsync(factory, "login-ingles@example.com");
        var client = PageClient.Anonymous(factory);
        await InEnglishAsync(client);

        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Login"));
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "login-ingles@example.com",
            ["Password"] = "nao-e-essa",
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Contains("Invalid e-mail or password.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task WeakPasswordAtRegistration_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        await InEnglishAsync(client);

        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Register"));
        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "fraca-ingles@example.com",
            ["Password"] = "123456",
            ["ConfirmPassword"] = "123456",
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Contains("The password needs at least one uppercase letter (A-Z).", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
}
