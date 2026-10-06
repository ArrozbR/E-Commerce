using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.Application.Identity;
using KeycapStore.Infrastructure;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class LoginPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Senha#Forte123";

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", postgres.Container.GetConnectionString()));

        await factory.Services.MigrateDatabaseAsync();
        return factory;
    }

    private static async Task RegisterAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<IAccountService>();
        var result = await accounts.RegisterAsync(email, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password)
    {
        var html = await client.GetStringAsync("/Account/Login");
        var token = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;

        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["__RequestVerificationToken"] = token,
        }));
    }

    [Fact]
    public async Task Post_WithValidCredentials_SetsSecureCookieAndRedirects()
    {
        await using var factory = await CreateFactoryAsync();
        await RegisterAsync(factory, "login@example.com");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await PostLoginAsync(client, "login@example.com", Password);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Catalog", response.Headers.Location?.OriginalString);

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Identity.Application="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Post_WithWrongPassword_ShowsGenericError()
    {
        await using var factory = await CreateFactoryAsync();
        await RegisterAsync(factory, "errada@example.com");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await PostLoginAsync(client, "errada@example.com", "SenhaErrada#1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("E-mail ou senha inválidos.", html);
    }

    [Fact]
    public async Task Post_ThirdWrongPassword_LocksAccountEvenForTheRightPassword()
    {
        await using var factory = await CreateFactoryAsync();
        await RegisterAsync(factory, "bloqueio@example.com");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            var wrong = await PostLoginAsync(client, "bloqueio@example.com", "SenhaErrada#1");
            Assert.Contains("E-mail ou senha inválidos.", WebUtility.HtmlDecode(await wrong.Content.ReadAsStringAsync()));
        }

        var third = await PostLoginAsync(client, "bloqueio@example.com", "SenhaErrada#1");
        Assert.Contains("Muitas tentativas erradas.", WebUtility.HtmlDecode(await third.Content.ReadAsStringAsync()));

        var right = await PostLoginAsync(client, "bloqueio@example.com", Password);
        Assert.Equal(HttpStatusCode.OK, right.StatusCode);
        Assert.Contains("Muitas tentativas erradas.", WebUtility.HtmlDecode(await right.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task LoginThenLogout_MenuFollowsTheSession()
    {
        await using var factory = await CreateFactoryAsync();
        await RegisterAsync(factory, "menu@example.com");
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

        await PostLoginAsync(client, "menu@example.com", Password);
        var loggedIn = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog"));
        Assert.Contains("Olá, menu@example.com", loggedIn);
        Assert.Contains("Sair", loggedIn);

        var token = Regex.Match(loggedIn, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;
        var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);

        var loggedOut = WebUtility.HtmlDecode(await client.GetStringAsync("/Catalog"));
        Assert.DoesNotContain("Olá, menu@example.com", loggedOut);
        Assert.Contains("Entrar", loggedOut);
    }
}
