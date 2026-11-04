using System.Net;

using KeycapStore.Infrastructure.Identity;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Identity;

public class SessionRevocationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<HttpResponseMessage> DeleteAccountAsync(HttpClient client)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Account/Delete"));

        return await client.PostAsync("/Account/Delete", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Confirmation"] = PageClient.Password,
            ["__RequestVerificationToken"] = token,
        }));
    }

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

    private static async Task<bool> IsAdminAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = await users.FindByEmailAsync(email);
        return await users.IsInRoleAsync(user!, AdminCommand.AdminRole);
    }

    private static void AssertSentToLogin(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task DeletedAccount_OtherSessionOfTheSameAccount_IsSignedOutAtTheNextRequest()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var phone = await PageClient.LoggedInAsync(factory, "dois-aparelhos@example.com");
        var laptop = await PageClient.LoggedInAsync(factory, "dois-aparelhos@example.com");
        Assert.Equal(HttpStatusCode.OK, (await laptop.GetAsync("/Cart")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await DeleteAccountAsync(phone)).StatusCode);

        AssertSentToLogin(await laptop.GetAsync("/Cart"));
    }

    [Fact]
    public async Task CreateAdmin_PromotingALoggedInCustomer_EndsTheSessionsOpenedBefore()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "promovido@example.com");

        var result = await factory.Services.CreateAdminAsync("promovido@example.com");

        Assert.True(result.Succeeded);
        AssertSentToLogin(await client.GetAsync("/admin/orders"));
    }

    [Fact]
    public async Task CreateAdmin_ForAnAccountThatSignsInWithGoogle_IsRefused_AndTheGoogleSessionStaysACustomer()
    {
        var google = new FakeGoogleAccount { Id = "google-quase-admin", Email = "quase-admin@gmail.com" };
        await using var factory = await postgres.CreateMigratedFactoryAsync(google);
        var client = PageClient.Anonymous(factory);
        await SignInWithGoogleAsync(client);

        var result = await factory.Services.CreateAdminAsync("quase-admin@gmail.com");

        Assert.False(result.Succeeded);
        Assert.False(await IsAdminAsync(factory, "quase-admin@gmail.com"));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin/orders")).StatusCode);
    }
}
