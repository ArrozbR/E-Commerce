using System.Net;

using KeycapStore.IntegrationTests.Fixtures;

namespace KeycapStore.IntegrationTests.Catalog;

public class HomePageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<string> HomeAsync(HttpClient client) =>
        WebUtility.HtmlDecode(await client.GetStringAsync("/"));

    [Fact]
    public async Task Home_ShowsTheTypingKeyboard_WithTheBaseKitsOfTheCatalog()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await HomeAsync(PageClient.Anonymous(factory));

        Assert.Contains("data-showcase", html);
        Assert.Contains("data-words=\"qwerty|thock|wasd|clack|switch\"", html);
        Assert.Contains("data-key=\"P\"", html);
        Assert.Contains("data-showcase-kit data-keys=\"#1e1b4b\" data-legends=\"#a5f3fc\" data-accent=\"#22c55e\">", html);
        Assert.Contains("href=\"/Catalog/6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01/kit-aurora-base\"", html);
        Assert.Contains("data-showcase-kit data-keys=\"#0e7490\" data-legends=\"#ecfeff\" data-accent=\"#fbbf24\" hidden=\"hidden\">", html);
        Assert.DoesNotContain("data-keys=\"#a5f3fc\"", html);
    }

    [Fact]
    public async Task Home_ShowsThreeKitsToBuy_HowItWorks_AndTheWayToTheCatalog()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await HomeAsync(PageClient.Anonymous(factory));

        Assert.Equal(3, html.Split("class=\"card card-interactive").Length - 1);
        Assert.Contains("id=\"como-funciona\"", html);
        Assert.Contains("Se o lote não atingir o mínimo, a loja cancela o pedido e devolve o valor.", html);
        Assert.Contains("href=\"/Catalog\"", html);
        Assert.Matches("<a class=\"logo [^>]*href=\"/\">", html);
    }

    [Fact]
    public async Task Home_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        var token = PageClient.TokenFrom(await client.GetStringAsync("/"));
        await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "en-US",
            ["returnUrl"] = "/",
            ["__RequestVerificationToken"] = token,
        }));

        var html = await HomeAsync(client);

        Assert.Contains("Keys you can <span class=\"glow-text text-brand\">feel</span>.", html);
        Assert.Contains("How it works", html);
        Assert.Contains("Pick your set", html);
        Assert.Contains(">Add to cart<", html);
        Assert.Contains("R$349.90", html);
    }
}
