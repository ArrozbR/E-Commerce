using System.Net;

using KeycapStore.IntegrationTests.Fixtures;

namespace KeycapStore.IntegrationTests.Catalog;

public class CatalogFilterPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<string> CatalogAsync(HttpClient client, string query)
    {
        var response = await client.GetAsync($"/Catalog{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static int Cards(string html) => html.Split("class=\"card card-interactive").Length - 1;

    [Fact]
    public async Task Search_WithoutAccent_FindsTheKits_AndKeepsWhatWasTyped()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await CatalogAsync(PageClient.Anonymous(factory), "?q=mare");

        Assert.Equal(2, Cards(html));
        Assert.Contains("<span>Kit Maré (base)</span>", html);
        Assert.DoesNotContain("<span>Kit Aurora (base)</span>", html);
        Assert.Contains("data-catalog-count>2 kits</p>", html);
        Assert.Contains("value=\"mare\"", html);
    }

    [Fact]
    public async Task KindAndInStock_Combine()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await CatalogAsync(PageClient.Anonymous(factory), "?kind=Modifiers&inStock=true");

        Assert.Equal(0, Cards(html));
        Assert.Contains("Nenhum kit encontrado com esses filtros.", html);
        Assert.Contains("<option value=\"Modifiers\" selected=\"selected\">", html);
        Assert.Contains("id=\"inStock\" name=\"inStock\" type=\"checkbox\" value=\"true\" class=\"check-input\" checked=\"checked\"", html);
    }

    [Fact]
    public async Task LowestPrice_PutsTheCheapestKitFirst()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await CatalogAsync(PageClient.Anonymous(factory), "?sort=price-asc");

        Assert.True(html.IndexOf("<span>Kit Zéfiro (teclas de função)</span>", StringComparison.Ordinal) < html.IndexOf("<span>Kit Aurora (base)</span>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Colorway_ShowsTheKitsOfThatSet()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await CatalogAsync(PageClient.Anonymous(factory), "?colorway=1e1b4b-a5f3fc-22c55e");

        Assert.Equal(2, Cards(html));
        Assert.Contains("<span>Kit Aurora (base)</span>", html);
        Assert.Contains("<span>Kit Aurora (novelties)</span>", html);
        Assert.Contains("value=\"1e1b4b-a5f3fc-22c55e\" checked=\"checked\"", html);
    }

    [Fact]
    public async Task InvalidFilters_AreIgnored_InsteadOfFailing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await CatalogAsync(PageClient.Anonymous(factory), "?kind=Bogus&sort=hack&colorway=nao-existe");

        Assert.Contains("Nenhum kit encontrado com esses filtros.", html);
        var everything = await CatalogAsync(PageClient.Anonymous(factory), "?kind=Bogus&sort=hack");
        Assert.Equal(6, Cards(everything));
    }

    [Fact]
    public async Task Filters_InEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);
        await client.PostAsync("/Language/Set", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["culture"] = "en-US",
            ["returnUrl"] = "/Catalog",
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Catalog")),
        }));

        var html = await CatalogAsync(client, "?q=aurora");

        Assert.Contains(">Search</label>", html);
        Assert.Contains(">Lowest price</option>", html);
        Assert.Contains("Available only", html);
        Assert.Contains("data-catalog-count>2 kits</p>", html);
    }
}
