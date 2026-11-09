using System.Net;

using KeycapStore.IntegrationTests.Fixtures;

namespace KeycapStore.IntegrationTests.Smoke;

public class IconTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("/icon.svg", "image/svg+xml")]
    [InlineData("/favicon.ico", "image/x-icon")]
    [InlineData("/apple-touch-icon.png", "image/png")]
    public async Task Icon_IsServed(string path, string contentType)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EveryPage_LinksTheIcons()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await PageClient.Anonymous(factory).GetStringAsync("/Catalog");

        Assert.Matches("rel=\"icon\" href=\"/icon\\.svg\\?v=[^\"]+\" type=\"image/svg\\+xml\"", html);
        Assert.Matches("rel=\"icon\" href=\"/favicon\\.ico\\?v=[^\"]+\"", html);
        Assert.Matches("rel=\"apple-touch-icon\" href=\"/apple-touch-icon\\.png\\?v=[^\"]+\"", html);
    }
}
