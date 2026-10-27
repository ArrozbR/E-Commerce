using System.Net;

using KeycapStore.IntegrationTests.Fixtures;

namespace KeycapStore.IntegrationTests.Localization;

public class LanguageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<HttpResponseMessage> ChooseAsync(HttpClient client, string culture, string returnUrl)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));
        return await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = culture,
            ["returnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static string? CultureCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith(".AspNetCore.Culture=", StringComparison.Ordinal))
            : null;

    [Fact]
    public async Task FirstVisit_IsInPortuguese()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = WebUtility.HtmlDecode(await PageClient.Anonymous(factory).GetStringAsync("/Catalog"));

        Assert.Contains("lang=\"pt-BR\"", html);
        Assert.Contains(">Catálogo</a>", html);
        Assert.Contains(">Criar conta</a>", html);
    }

    [Fact]
    public async Task FirstVisit_WithAnEnglishBrowser_IsStillInPortuguese()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog"));

        Assert.Contains(">Catálogo</a>", html);
    }

    [Fact]
    public async Task ChoosingEnglish_SavesTheCookie_AndReturnsToTheSamePage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);

        var response = await ChooseAsync(client, "en-US", "/Catalog");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Catalog", response.Headers.Location?.OriginalString);
        Assert.Contains("c%3Den-US%7Cuic%3Den-US", CultureCookie(response));

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog"));
        Assert.Contains("lang=\"en-US\"", html);
        Assert.Contains(">Catalog</a>", html);
        Assert.Contains(">Create account</a>", html);
    }

    [Fact]
    public async Task ChoosingPortugueseAgain_GoesBackToPortuguese()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        await ChooseAsync(client, "en-US", "/Catalog");

        await ChooseAsync(client, "pt-BR", "/Catalog");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog"));
        Assert.Contains(">Catálogo</a>", html);
    }

    [Fact]
    public async Task UnknownLanguage_FallsBackToPortuguese()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);

        var response = await ChooseAsync(client, "fr-FR", "/Catalog");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("c%3Dpt-BR%7Cuic%3Dpt-BR", CultureCookie(response));
    }

    [Theory]
    [InlineData("https://golpe.example/")]
    [InlineData("//golpe.example/")]
    public async Task ReturnUrlOutsideTheStore_GoesToTheHomePage(string returnUrl)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);

        var response = await ChooseAsync(client, "en-US", returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task CatalogInEnglish_ShowsPricesInReais_InTheAmericanFormat()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        await ChooseAsync(client, "en-US", "/Catalog");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog"));

        Assert.Contains(">R$349.90<", html);
        Assert.DoesNotContain(">$", html);
        Assert.DoesNotContain("US$", html);
    }
}
