using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.Application.Identity;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Fixtures;

public static class PageClient
{
    public const string Password = "Senha#Forte123";

    public static string TokenFrom(string html) =>
        Regex.Match(html, @"name=""__RequestVerificationToken"" type=""hidden"" value=""([^""]+)""").Groups[1].Value;

    public static HttpClient Anonymous(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
        });

    public static async Task<HttpClient> LoggedInAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, Password);
        }

        var client = Anonymous(factory);
        var token = TokenFrom(await client.GetStringAsync("/Account/Login"));
        var login = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token,
        }));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        return client;
    }
}
