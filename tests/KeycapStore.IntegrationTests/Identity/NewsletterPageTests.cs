using System.Net;

using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class NewsletterPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<bool> SubscribedAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.NewsletterSubscriptions.AnyAsync(s => s.CustomerId == user!.Id);
    }

    private static async Task<HttpResponseMessage> RegisterAsync(HttpClient client, string email, bool receiveNews)
    {
        var fields = new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = PageClient.Password,
            ["ConfirmPassword"] = PageClient.Password,
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Account/Register")),
        };
        if (receiveNews)
        {
            fields["ReceiveNews"] = "true";
        }

        return await client.PostAsync("/Account/Register", new FormUrlEncodedContent(fields));
    }

    private static async Task<HttpResponseMessage> ToggleAsync(HttpClient client, bool receive) =>
        await client.PostAsync("/Account/Newsletter", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["receive"] = receive ? "true" : "false",
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Account/Delete")),
        }));

    [Fact]
    public async Task Register_TheNewsBoxStartsUnchecked()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = await PageClient.Anonymous(factory).GetStringAsync("/Account/Register");

        Assert.Contains("name=\"ReceiveNews\"", html);
        Assert.DoesNotContain("checked", html.Substring(html.IndexOf("name=\"ReceiveNews\"", StringComparison.Ordinal), 120));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Register_SubscribesOnlyWhenTheBoxIsChecked(bool receiveNews)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var email = $"cadastro-novidades-{receiveNews}@example.com".ToLowerInvariant();

        var response = await RegisterAsync(PageClient.Anonymous(factory), email, receiveNews);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(receiveNews, await SubscribedAsync(factory, email));
    }

    [Fact]
    public async Task AccountPage_TurnsNewsOnAndOff()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        const string email = "conta-novidades@example.com";
        var client = await PageClient.LoggedInAsync(factory, email);

        Assert.Contains("data-newsletter-off", await client.GetStringAsync("/Account/Delete"));

        var on = await ToggleAsync(client, receive: true);
        Assert.Equal(HttpStatusCode.Redirect, on.StatusCode);
        var page = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete"));
        Assert.Contains("data-newsletter-on", page);
        Assert.Contains("Novidades ligadas.", page);
        Assert.True(await SubscribedAsync(factory, email));

        await ToggleAsync(client, receive: false);
        Assert.Contains("data-newsletter-off", await client.GetStringAsync("/Account/Delete"));
        Assert.False(await SubscribedAsync(factory, email));
    }

    [Fact]
    public async Task Anonymous_CannotToggleNews()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);

        var response = await client.PostAsync("/Account/Newsletter", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["receive"] = "true",
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Account/Login")),
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }
}
