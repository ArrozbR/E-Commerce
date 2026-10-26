using System.Net;

using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class SavedAddressCheckoutPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private static readonly ShippingAddress Paulista =
        new("Ana Souza", "Avenida Paulista", "1000", "Apto 12", "Bela Vista", "São Paulo", "SP", "01310100");

    private static async Task AddKitAuroraAsync(HttpClient client)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        var add = await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = KitAurora,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, add.StatusCode);
    }

    private static async Task<HttpResponseMessage> PostCuritibaAsync(HttpClient client, bool saveAddress)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Checkout"));
        var form = new Dictionary<string, string>
        {
            ["RecipientName"] = "Bruno Lima",
            ["PostalCode"] = "80020-310",
            ["Street"] = "Rua XV de Novembro",
            ["Number"] = "200",
            ["District"] = "Centro",
            ["City"] = "Curitiba",
            ["State"] = "PR",
            ["__RequestVerificationToken"] = token,
        };
        if (saveAddress)
        {
            form["SaveAddress"] = "true";
        }

        return await client.PostAsync("/Checkout", new FormUrlEncodedContent(form));
    }

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

    [Fact]
    public async Task Checkout_WithTheBoxChecked_SavesTheAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "salvar@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostCuritibaAsync(client, saveAddress: true);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var saved = await SavedAddressOfAsync(factory, "salvar@example.com");
        Assert.NotNull(saved);
        Assert.Equal("Rua XV de Novembro", saved.Street);
        Assert.Equal("80020310", saved.PostalCode);
    }

    [Fact]
    public async Task Checkout_WithoutTheBox_DoesNotSaveTheAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "nao-salvar@example.com");
        await AddKitAuroraAsync(client);

        var response = await PostCuritibaAsync(client, saveAddress: false);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Null(await SavedAddressOfAsync(factory, "nao-salvar@example.com"));
    }

    [Fact]
    public async Task Checkout_ThatFails_DoesNotSaveTheAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "carrinho-vazio@example.com");

        var response = await PostCuritibaAsync(client, saveAddress: true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(await SavedAddressOfAsync(factory, "carrinho-vazio@example.com"));
    }

    [Fact]
    public async Task CheckoutPage_WithASavedAddress_ComesFilledIn()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "preenchido@example.com");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var customerId = await CustomerIdAsync(factory, "preenchido@example.com");
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync(customerId, Paulista);
        }

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Checkout"));

        Assert.Contains("Preenchido com o endereço salvo", html);
        Assert.Contains("value=\"Avenida Paulista\"", html);
        Assert.Contains("value=\"01310100\"", html);
    }

    [Fact]
    public async Task CheckoutPage_DoesNotShowAnotherCustomersAddress()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await PageClient.LoggedInAsync(factory, "dono-do-endereco@example.com");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var ownerId = await CustomerIdAsync(factory, "dono-do-endereco@example.com");
            await scope.ServiceProvider.GetRequiredService<ISavedAddressService>().SaveAsync(ownerId, Paulista);
        }
        var curious = await PageClient.LoggedInAsync(factory, "curioso-do-endereco@example.com");

        var html = WebUtility.HtmlDecode(await curious.GetStringAsync("/Checkout"));

        Assert.DoesNotContain("Avenida Paulista", html);
        Assert.DoesNotContain("Preenchido com o endereço salvo", html);
    }
}
