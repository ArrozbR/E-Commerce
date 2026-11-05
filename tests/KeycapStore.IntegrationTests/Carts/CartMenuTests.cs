using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using KeycapStore.IntegrationTests.Fixtures;

namespace KeycapStore.IntegrationTests.Carts;

public class CartMenuTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private const string Notice = "<span data-toast-message>Kit adicionado ao carrinho.</span>";

    private static Task<HttpResponseMessage> AddAsync(HttpClient client, string token, string productId, string? returnUrl = null)
    {
        var form = new Dictionary<string, string>
        {
            ["productId"] = productId,
            ["__RequestVerificationToken"] = token,
        };
        if (returnUrl is not null)
        {
            form["returnUrl"] = returnUrl;
        }

        return client.PostAsync("/Cart/Add", new FormUrlEncodedContent(form));
    }

    private static async Task<HttpResponseMessage> AddAsJsonAsync(HttpClient client, string token, string productId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Cart/Add")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["productId"] = productId,
                ["returnUrl"] = "/Catalog",
                ["__RequestVerificationToken"] = token,
            }),
        };
        request.Headers.Accept.ParseAdd("application/json");

        return await client.SendAsync(request);
    }

    private static string? CartCount(string html) =>
        Regex.Match(html, @"id=""cart-count""[^>]*>(\d+)<") is { Success: true } match ? match.Groups[1].Value : null;

    [Fact]
    public async Task CatalogForm_SendsThePageAsTheReturnUrl()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "volta-form@example.com");

        var html = await client.GetStringAsync("/Catalog");

        Assert.Contains("name=\"returnUrl\" value=\"/Catalog\"", html);
    }

    [Fact]
    public async Task Add_WithTheCatalogAsTheReturnUrl_GoesBack_AndShowsTheNoticeOnce()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "volta-catalogo@example.com");
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));

        var response = await AddAsync(client, token, KitAurora, "/Catalog");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Catalog", response.Headers.Location?.OriginalString);
        Assert.Contains(Notice, WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog")));
        Assert.DoesNotContain(Notice, WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog")));
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    public async Task Add_WithAReturnUrlOutsideTheStore_GoesToTheCart_WithoutTheNotice(string returnUrl)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, $"volta-fora-{returnUrl.Length}@example.com");
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));

        var response = await AddAsync(client, token, KitAurora, returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Cart", response.Headers.Location?.OriginalString);
        var cart = WebUtility.HtmlDecode(await client.GetStringAsync("/Cart"));
        Assert.Contains("Kit Aurora (base)", cart);
        Assert.DoesNotContain(Notice, cart);
    }

    [Fact]
    public async Task Add_WhenRefused_GoesToTheCartWithTheMessage_EvenWithAReturnUrl()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Menu");
        var client = await PageClient.LoggedInAsync(factory, "volta-recusa@example.com");
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        await AddAsync(client, token, KitAurora, "/Catalog");
        await client.GetStringAsync("/Catalog");

        var response = await AddAsync(client, token, preOrder.ToString(), "/Catalog");

        Assert.Equal("/Cart", response.Headers.Location?.OriginalString);
        var cart = WebUtility.HtmlDecode(await client.GetStringAsync("/Cart"));
        Assert.Contains("Kits de pré-venda são comprados sozinhos.", cart);
        Assert.DoesNotContain(Notice, cart);
    }

    [Fact]
    public async Task Add_AskingForJson_ReturnsTheNewCount_WithoutRedirectOrNotice()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "json-contador@example.com");
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));

        var first = await AddAsJsonAsync(client, token, KitAurora);
        var second = await AddAsJsonAsync(client, token, KitAurora);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal("application/json", first.Content.Headers.ContentType?.MediaType);
        Assert.Equal(1, (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32());
        Assert.Equal(2, (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("count").GetInt32());
        Assert.DoesNotContain(Notice, WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog")));
    }

    [Fact]
    public async Task Add_AskingForJson_WhenRefused_Returns409WithTheMessage_AndNothingForTheCartPage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Json");
        var client = await PageClient.LoggedInAsync(factory, "json-recusa@example.com");
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        await AddAsJsonAsync(client, token, KitAurora);

        var response = await AddAsJsonAsync(client, token, preOrder.ToString());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var message = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString();
        Assert.StartsWith("Kits de pré-venda são comprados sozinhos.", message);
        Assert.DoesNotContain("Kits de pré-venda são comprados sozinhos.", WebUtility.HtmlDecode(await client.GetStringAsync("/Cart")));
    }

    [Fact]
    public async Task Menu_WithAnEmptyCart_HasAHiddenCount_AndTheTextsTheScriptFillsIn()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "contador-vazio@example.com");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog"));

        Assert.Contains("id=\"cart-count\" class=\"cart-count\" aria-hidden=\"true\" hidden=\"hidden\"></span>", html);
        Assert.Contains("data-one=\"(1 item)\" data-many=\"({0} itens)\"", html);
    }

    [Fact]
    public async Task Menu_ShowsHowManyUnitsAreInTheCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        Assert.Null(CartCount(await PageClient.Anonymous(factory).GetStringAsync("/Catalog")));

        var client = await PageClient.LoggedInAsync(factory, "contador@example.com");
        var html = await client.GetStringAsync("/Catalog");
        Assert.Null(CartCount(html));

        var token = PageClient.TokenFrom(html);
        await AddAsync(client, token, KitAurora);
        Assert.Equal("1", CartCount(await client.GetStringAsync("/Catalog")));

        await AddAsync(client, token, KitAurora);
        await AddAsync(client, token, KitAurora);
        html = WebUtility.HtmlDecode(await client.GetStringAsync("/Orders"));

        Assert.Equal("3", CartCount(html));
        Assert.Contains("(3 itens)", html);
    }
}
