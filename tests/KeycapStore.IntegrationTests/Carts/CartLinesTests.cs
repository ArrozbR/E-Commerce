using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Carts;

public class CartLinesTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private static async Task AddAsync(HttpClient client, string productId, int times = 1)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        for (var i = 0; i < times; i++)
        {
            await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["productId"] = productId,
                ["__RequestVerificationToken"] = token,
            }));
        }
    }

    private static async Task<HttpResponseMessage> PostAsJsonAsync(HttpClient client, string path, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Cart"));
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Accept.ParseAdd("application/json");

        return await client.SendAsync(request);
    }

    [Fact]
    public async Task CartPage_ShowsEachKitWithItsDrawing_TheStepper_AndTheSummary()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "carrinho-novo@example.com");
        await AddAsync(client, KitAurora, 2);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Cart"));

        Assert.Contains($"data-cart-line=\"{KitAurora}\"", html);
        Assert.Contains("data-key=\"Q\"", html);
        Assert.Contains("name=\"quantity\" value=\"1\" class=\"stepper-button\" data-step=\"-1\"", html);
        Assert.Contains("name=\"quantity\" value=\"3\" class=\"stepper-button\" data-step=\"1\"", html);
        Assert.Contains("data-quantity data-max=\"10\"", html);
        Assert.Contains("(<span data-cart-units>2</span>)", html);
        Assert.Contains("<span data-cart-total>R$ 699,80</span>", html);
        Assert.Matches("data-state=\"current\" aria-current=\"step\">[\\s\\S]*?<span>Carrinho</span>", html);
        Assert.Contains("href=\"/Checkout\"", html);
    }

    [Fact]
    public async Task Stepper_AtTheStockLimit_CannotGoUp()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var product = new Product("Kit Duas Unidades", 100m, 2, TestPackage.Standard);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        var client = await PageClient.LoggedInAsync(factory, "carrinho-limite@example.com");
        await AddAsync(client, product.Id.ToString(), 2);

        var html = await client.GetStringAsync("/Cart");

        Assert.Contains("value=\"3\" class=\"stepper-button\" data-step=\"1\" disabled=\"disabled\"", html);
        Assert.Contains("data-quantity data-max=\"2\"", html);
    }

    [Fact]
    public async Task ChangeQuantity_AskingForJson_ReturnsTheWholeCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "carrinho-json@example.com");
        await AddAsync(client, KitAurora);

        var response = await PostAsJsonAsync(client, "/Cart/ChangeQuantity", new() { ["productId"] = KitAurora, ["quantity"] = "3" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, json.GetProperty("count").GetInt32());
        Assert.Equal("R$ 1.049,70", json.GetProperty("total").GetString()!.Replace(' ', ' '));
        Assert.False(json.GetProperty("hasUnavailableItems").GetBoolean());
        var line = Assert.Single(json.GetProperty("lines").EnumerateArray());
        Assert.Equal(KitAurora, line.GetProperty("productId").GetString());
        Assert.Equal(3, line.GetProperty("quantity").GetInt32());
        Assert.Equal(10, line.GetProperty("max").GetInt32());
        Assert.Equal("R$ 1.049,70", line.GetProperty("lineTotal").GetString()!.Replace(' ', ' '));
    }

    [Fact]
    public async Task ChangeQuantity_AskingForJson_PastTheLimit_Returns409_AndKeepsTheQuantity()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "carrinho-json-limite@example.com");
        await AddAsync(client, KitAurora);

        var response = await PostAsJsonAsync(client, "/Cart/ChangeQuantity", new() { ["productId"] = KitAurora, ["quantity"] = "11" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Cada kit vai no máximo 10 unidades por pedido.", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
        Assert.Contains("(<span data-cart-units>1</span>)", WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")));
    }

    [Fact]
    public async Task Remove_AskingForJson_ReturnsAnEmptyCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "carrinho-json-remove@example.com");
        await AddAsync(client, KitAurora);

        var response = await PostAsJsonAsync(client, "/Cart/Remove", new() { ["productId"] = KitAurora });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, json.GetProperty("count").GetInt32());
        Assert.Empty(json.GetProperty("lines").EnumerateArray());
        Assert.Contains("Seu carrinho está vazio.", WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")));
    }
}
