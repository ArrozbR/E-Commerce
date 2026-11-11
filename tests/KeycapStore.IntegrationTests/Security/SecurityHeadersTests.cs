using System.Text.RegularExpressions;

using KeycapStore.IntegrationTests.Fixtures;

namespace KeycapStore.IntegrationTests.Security;

public partial class SecurityHeadersTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [GeneratedRegex(@"<script(?![^>]*\bsrc=)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineScript();

    [GeneratedRegex(@"\son[a-z]+\s*=\s*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex InlineHandler();

    [Theory]
    [InlineData("/")]
    [InlineData("/Catalog")]
    [InlineData("/Account/Login")]
    [InlineData("/css/app.css")]
    [InlineData("/pagina-que-nao-existe")]
    public async Task EveryResponse_CarriesTheSecurityHeaders(string path)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync(path);

        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        var scriptSrc = csp.Split(';').Select(d => d.Trim()).Single(d => d.StartsWith("script-src ", StringComparison.Ordinal));
        Assert.DoesNotContain("'unsafe-inline'", scriptSrc);
        Assert.DoesNotContain("'unsafe-eval'", scriptSrc);
        Assert.Contains("https://js.stripe.com", scriptSrc);
        Assert.Contains("frame-ancestors 'none'", csp);
        Assert.Contains("https://hooks.stripe.com", csp);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("strict-origin-when-cross-origin", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact]
    public async Task ThePasswordResetPage_KeepsItsStricterReferrerPolicy()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync("/Account/ResetPassword?userId=x&token=y");

        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Catalog")]
    [InlineData("/Catalog/6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01/kit-aurora-base")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    public async Task RenderedPages_HaveNoInlineScriptsOrHandlers(string path)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await PageClient.Anonymous(factory).GetStringAsync(path);

        Assert.DoesNotMatch(InlineScript(), html);
        Assert.DoesNotMatch(InlineHandler(), html);
    }

    [Fact]
    public async Task TheOrderPage_UsesDataConfirm_InsteadOfInlineHandlers()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "csp-conta@example.com");

        var html = await client.GetStringAsync("/Account/Delete");

        Assert.Contains("data-confirm=", html);
        Assert.DoesNotMatch(InlineScript(), html);
        Assert.DoesNotMatch(InlineHandler(), html);
    }
}
