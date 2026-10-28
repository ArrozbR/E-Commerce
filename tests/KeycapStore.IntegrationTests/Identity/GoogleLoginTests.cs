using System.Net;

using KeycapStore.Application.Identity;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KeycapStore.IntegrationTests.Identity;

public class GoogleLoginTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<HttpResponseMessage> SignInWithGoogleAsync(HttpClient client)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Login"));
        var toGoogle = await client.PostAsync("/Account/ExternalLogin", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, toGoogle.StatusCode);

        return await client.GetAsync(toGoogle.Headers.Location);
    }

    private static async Task<(bool HasPassword, int Logins)> AccountStateAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync(email);
        return (await users.HasPasswordAsync(user!), (await users.GetLoginsAsync(user!)).Count);
    }

    private static async Task<string> CatalogAsync(HttpClient client) =>
        WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog"));

    [Fact]
    public async Task FirstTimeWithGoogle_CreatesAnAccountWithoutPassword_AndSignsIn()
    {
        var google = new FakeGoogleAccount { Id = "google-novo", Email = "novo@gmail.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = PageClient.Anonymous(factory);

        var response = await SignInWithGoogleAsync(client);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Catalog", response.Headers.Location?.OriginalString);
        Assert.Contains("Olá, novo@gmail.com", await CatalogAsync(client));
        Assert.Equal((false, 1), await AccountStateAsync(factory, "novo@gmail.com"));
    }

    [Fact]
    public async Task SecondTimeWithGoogle_UsesTheSameAccount()
    {
        var google = new FakeGoogleAccount { Id = "google-volta", Email = "volta@gmail.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        await SignInWithGoogleAsync(PageClient.Anonymous(factory));
        var client = PageClient.Anonymous(factory);

        await SignInWithGoogleAsync(client);

        Assert.Contains("Olá, volta@gmail.com", await CatalogAsync(client));
        Assert.Equal((false, 1), await AccountStateAsync(factory, "volta@gmail.com"));
    }

    [Fact]
    public async Task GoogleWithTheEmailOfAPasswordAccount_IsRefused_AndNothingIsLinked()
    {
        var google = new FakeGoogleAccount { Id = "google-golpe", Email = "ja-tem-senha@gmail.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync("ja-tem-senha@gmail.com", PageClient.Password);
        }
        var client = PageClient.Anonymous(factory);

        var response = await SignInWithGoogleAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Já existe uma conta com este e-mail.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.DoesNotContain("Olá, ja-tem-senha@gmail.com", await CatalogAsync(client));
        Assert.Equal((true, 0), await AccountStateAsync(factory, "ja-tem-senha@gmail.com"));
    }

    [Fact]
    public async Task AdminAccount_CannotSignInWithGoogle()
    {
        var google = new FakeGoogleAccount { Id = "google-admin", Email = "virou-admin@gmail.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        await SignInWithGoogleAsync(PageClient.Anonymous(factory));
        Assert.True((await factory.Services.CreateAdminAsync("virou-admin@gmail.com")).Succeeded);
        var client = PageClient.Anonymous(factory);

        var response = await SignInWithGoogleAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Contas de administrador entram só com a senha.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.DoesNotContain("Olá, virou-admin@gmail.com", await CatalogAsync(client));
    }

    [Fact]
    public async Task GoogleWithoutEmail_IsRefused()
    {
        var google = new FakeGoogleAccount { Id = "google-sem-email", Email = null };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = PageClient.Anonymous(factory);

        var response = await SignInWithGoogleAsync(client);

        Assert.Contains("Não foi possível entrar com o Google.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task OpeningTheReturnAddressDirectly_DoesNotSignIn()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = PageClient.Anonymous(factory);

        var response = await client.GetAsync("/Account/ExternalLoginCallback");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Não foi possível entrar com o Google.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task LoginPage_ShowsTheGoogleButton()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var html = WebUtility.HtmlDecode(await PageClient.Anonymous(factory).GetStringAsync("/Account/Login"));

        Assert.Contains("Entrar com o Google", html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("segredo-colado-no-lugar-do-id")]
    public async Task App_WithoutAValidGoogleClientId_RefusesToStart(string clientId)
    {
        await using var factory = postgres.CreateFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("Authentication:Google:ClientId", clientId));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Authentication:Google:ClientId", error.Message);
    }

    [Fact]
    public async Task App_WithoutTheGoogleSecret_RefusesToStart()
    {
        await using var factory = postgres.CreateFactory()
            .WithWebHostBuilder(builder => builder.UseSetting("Authentication:Google:ClientSecret", ""));

        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Authentication:Google:ClientSecret", error.Message);
    }
}
