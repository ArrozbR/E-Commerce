using System.Net;

using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KeycapStore.IntegrationTests.Security;

public class AccountRateLimitTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private async Task<WebApplicationFactory<Program>> FactoryWithLimitsAsync(int login = 1000, int register = 1000, int forgot = 1000)
    {
        var factory = (await postgres.CreateMigratedFactoryAsync(new FakeEmailSender())).WithWebHostBuilder(builder =>
        {
            builder.UseSetting("AccountLimits:LoginPerMinute", login.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("AccountLimits:RegisterPerHour", register.ToString(System.Globalization.CultureInfo.InvariantCulture));
            builder.UseSetting("AccountLimits:ForgotPasswordPer15Minutes", forgot.ToString(System.Globalization.CultureInfo.InvariantCulture));
        });
        return factory;
    }

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync(path));
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email) =>
        PostAsync(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = PageClient.Password + "x" });

    [Fact]
    public async Task Login_AfterTheLimit_Gets429WithTheStorePage_AndRetryAfter()
    {
        await using var factory = await FactoryWithLimitsAsync(login: 3);
        var client = PageClient.Anonymous(factory);

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, $"limite-{i}@example.com")).StatusCode);
        }

        var blocked = await LoginAsync(client, "limite-extra@example.com");

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.True(blocked.Headers.RetryAfter is not null);
        Assert.Contains("error-key", await blocked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_ThePageItselfIsNotLimited()
    {
        await using var factory = await FactoryWithLimitsAsync(login: 1);
        var client = PageClient.Anonymous(factory);

        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).StatusCode);
        }
    }

    [Fact]
    public async Task Register_AfterTheLimit_Gets429()
    {
        await using var factory = await FactoryWithLimitsAsync(register: 2);
        var client = PageClient.Anonymous(factory);

        for (var i = 0; i < 2; i++)
        {
            var created = await PostAsync(client, "/Account/Register", new()
            {
                ["Email"] = $"limite-cadastro-{i}@example.com",
                ["Password"] = PageClient.Password,
                ["ConfirmPassword"] = PageClient.Password,
            });
            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Catalog")),
            }));
        }

        var blocked = await PostAsync(client, "/Account/Register", new()
        {
            ["Email"] = "limite-cadastro-extra@example.com",
            ["Password"] = PageClient.Password,
            ["ConfirmPassword"] = PageClient.Password,
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_AfterTheLimit_Gets429()
    {
        await using var factory = await FactoryWithLimitsAsync(forgot: 2);
        var client = PageClient.Anonymous(factory);

        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "/Account/ForgotPassword", new() { ["Email"] = $"limite-senha-{i}@example.com" })).StatusCode);
        }

        var blocked = await PostAsync(client, "/Account/ForgotPassword", new() { ["Email"] = "limite-senha-extra@example.com" });

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }

    [Fact]
    public async Task TheLimitsOfEachForm_AreSeparate()
    {
        await using var factory = await FactoryWithLimitsAsync(login: 1, forgot: 1);
        var client = PageClient.Anonymous(factory);

        await LoginAsync(client, "separado@example.com");
        Assert.Equal(HttpStatusCode.TooManyRequests, (await LoginAsync(client, "separado@example.com")).StatusCode);

        var forgot = await PostAsync(client, "/Account/ForgotPassword", new() { ["Email"] = "separado@example.com" });
        Assert.Equal(HttpStatusCode.Redirect, forgot.StatusCode);
    }

    [Fact]
    public void Defaults_AreTheModerateNumbers_AndTheAppSettingsDoNotOverrideThem()
    {
        var limits = new KeycapStore.Web.RateLimiting.AccountLimitOptions();
        var appSettings = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

        Assert.Equal(10, limits.LoginPerMinute);
        Assert.Equal(5, limits.RegisterPerHour);
        Assert.Equal(5, limits.ForgotPasswordPer15Minutes);
        Assert.DoesNotContain("AccountLimits", appSettings);
    }
}
