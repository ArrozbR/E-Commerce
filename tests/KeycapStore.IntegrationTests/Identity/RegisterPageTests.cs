using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.Infrastructure;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class RegisterPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string Password = "Senha#Forte123";

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var html = await client.GetStringAsync("/Account/Register");
        var match = Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""");

        Assert.True(match.Success, "Não achei o código do antiforgery na página.");
        return match.Groups[1].Value;
    }

    [Fact]
    public async Task Post_WithValidData_CreatesUserAndRedirectsToCatalog()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await GetAntiforgeryTokenAsync(client);

        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "pagina@example.com",
            ["Password"] = Password,
            ["ConfirmPassword"] = Password,
            ["__RequestVerificationToken"] = token,
        }));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Catalog", response.Headers.Location?.OriginalString);

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.NotNull(await userManager.FindByEmailAsync("pagina@example.com"));
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_IsRejected()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.PostAsync("/Account/Register", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = "falso@example.com",
            ["Password"] = Password,
            ["ConfirmPassword"] = Password,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
