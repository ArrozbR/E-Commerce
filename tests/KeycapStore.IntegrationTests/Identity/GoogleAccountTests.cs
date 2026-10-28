using System.Net;

using KeycapStore.Infrastructure.Identity;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class GoogleAccountTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<HttpResponseMessage> GoThroughGoogleAsync(HttpClient client, string tokenPage, string action)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync(tokenPage));
        var toGoogle = await client.PostAsync(action, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, toGoogle.StatusCode);

        return await client.GetAsync(toGoogle.Headers.Location);
    }

    private static Task<HttpResponseMessage> LinkGoogleAsync(HttpClient client) =>
        GoThroughGoogleAsync(client, "/Account/Delete", "/Account/LinkGoogle");

    private static Task<HttpResponseMessage> SignInWithGoogleAsync(HttpClient client) =>
        GoThroughGoogleAsync(client, "/Account/Login", "/Account/ExternalLogin");

    private static async Task<int> GoogleLoginsAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync(email);
        return user is null ? -1 : (await users.GetLoginsAsync(user)).Count;
    }

    private static async Task<string> MyAccountAsync(HttpClient client) =>
        WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Delete"));

    private static async Task<HttpResponseMessage> DeleteAsync(HttpClient client, string confirmation)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Delete"));
        return await client.PostAsync("/Account/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Confirmation"] = confirmation,
            ["__RequestVerificationToken"] = token,
        }));
    }

    [Fact]
    public async Task PasswordAccount_ConnectsGoogle_AndThenSignsInWithIt()
    {
        var google = new FakeGoogleAccount { Id = "google-conecta", Email = "conecta@example.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = await PageClient.LoggedInAsync(factory, "conecta@example.com");
        Assert.Contains("Conectar o Google", await MyAccountAsync(client));

        var response = await LinkGoogleAsync(client);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var page = await MyAccountAsync(client);
        Assert.Contains("Conta Google conectada.", page);
        Assert.Contains("Sua conta está conectada ao Google.", page);
        Assert.Equal(1, await GoogleLoginsAsync(factory, "conecta@example.com"));

        var other = PageClient.Anonymous(factory);
        await SignInWithGoogleAsync(other);
        Assert.Contains("Olá, conecta@example.com", WebUtility.HtmlDecode(await other.GetStringAsync("/Catalog")));
    }

    [Fact]
    public async Task ConnectingAGoogleAccountWithAnotherEmail_IsRefused()
    {
        var google = new FakeGoogleAccount { Id = "google-outro-email", Email = "outra-pessoa@gmail.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = await PageClient.LoggedInAsync(factory, "dono@example.com");

        await LinkGoogleAsync(client);

        Assert.Contains("O e-mail da conta Google precisa ser o mesmo desta conta.", await MyAccountAsync(client));
        Assert.Equal(0, await GoogleLoginsAsync(factory, "dono@example.com"));
    }

    [Fact]
    public async Task ConnectingAGoogleAccountWithTheSameEmailInCapitals_Works()
    {
        var google = new FakeGoogleAccount { Id = "google-maiusculas", Email = "Maiusculas@Example.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = await PageClient.LoggedInAsync(factory, "maiusculas@example.com");

        await LinkGoogleAsync(client);

        Assert.Equal(1, await GoogleLoginsAsync(factory, "maiusculas@example.com"));
    }

    [Fact]
    public async Task Admin_CannotConnectGoogle()
    {
        var google = new FakeGoogleAccount { Id = "google-do-admin", Email = "admin-conecta@example.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = await PageClient.LoggedInAsync(factory, "admin-conecta@example.com");
        Assert.True((await factory.Services.CreateAdminAsync("admin-conecta@example.com")).Succeeded);

        await LinkGoogleAsync(client);

        Assert.Contains("Contas de administrador entram só com a senha.", await MyAccountAsync(client));
        Assert.Equal(0, await GoogleLoginsAsync(factory, "admin-conecta@example.com"));
    }

    [Fact]
    public async Task GoogleOnlyAccount_DeletesByTypingItsEmail()
    {
        var google = new FakeGoogleAccount { Id = "google-sai", Email = "so-google@gmail.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = PageClient.Anonymous(factory);
        await SignInWithGoogleAsync(client);
        Assert.Contains("Para confirmar, digite o e-mail desta conta", await MyAccountAsync(client));

        var response = await DeleteAsync(client, " So-Google@gmail.com ");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Deleted", response.Headers.Location?.OriginalString);
        Assert.Equal(-1, await GoogleLoginsAsync(factory, "so-google@gmail.com"));
    }

    [Fact]
    public async Task GoogleOnlyAccount_WithAnotherEmailTyped_IsNotDeleted()
    {
        var google = new FakeGoogleAccount { Id = "google-fica", Email = "fica@gmail.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = PageClient.Anonymous(factory);
        await SignInWithGoogleAsync(client);

        var response = await DeleteAsync(client, "outro@gmail.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("O e-mail digitado não é o desta conta.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(1, await GoogleLoginsAsync(factory, "fica@gmail.com"));
    }
}
