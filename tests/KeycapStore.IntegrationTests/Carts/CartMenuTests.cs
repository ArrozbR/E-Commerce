using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.IntegrationTests.Fixtures;

namespace KeycapStore.IntegrationTests.Carts;

public class CartMenuTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private const string Notice = "Kit adicionado ao carrinho.";

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
