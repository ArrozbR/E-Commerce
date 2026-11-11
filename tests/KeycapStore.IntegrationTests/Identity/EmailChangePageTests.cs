using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Identity;

public partial class EmailChangePageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [GeneratedRegex(@"https://localhost:7290(?<path>/Account/ConfirmEmailChange\?userId=[^&\s]+&token=[^\s""]+)")]
    private static partial Regex ConfirmLink();

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string formPage, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync(formPage));
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private static Task<HttpResponseMessage> AskAsync(HttpClient client, string newEmail, string? password = null) =>
        PostAsync(client, "/Account/ChangeEmail", "/Account/ChangeEmail", new()
        {
            ["NewEmail"] = newEmail,
            ["CurrentPassword"] = password ?? PageClient.Password,
        });

    private static async Task<string> ConfirmPathAsync(WebApplicationFactory<Program> factory, FakeEmailSender sender, string address)
    {
        await DispatchAsync(factory);
        var match = ConfirmLink().Match(Assert.Single(sender.Sent, m => m.To == address).TextBody);
        Assert.True(match.Success, "O e-mail não tem o link de confirmação.");
        return match.Groups["path"].Value;
    }

    private static Task<HttpResponseMessage> ConfirmAsync(HttpClient client, string confirmPath)
    {
        var query = System.Web.HttpUtility.ParseQueryString(new Uri("https://localhost" + confirmPath).Query);
        return PostAsync(client, "/Account/ConfirmEmailChange", confirmPath, new()
        {
            ["userId"] = query["userId"]!,
            ["token"] = query["token"]!,
        });
    }

    private static async Task<HttpStatusCode> LoginAsync(WebApplicationFactory<Program> factory, string email) =>
        (await PostAsync(PageClient.Anonymous(factory), "/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = PageClient.Password })).StatusCode;

    [Fact]
    public async Task AccountPage_ShowsTheEmail_AndTheChangeButton()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var client = await PageClient.LoggedInAsync(factory, "troca-botao@example.com");

        var html = await client.GetStringAsync("/Account/Delete");

        Assert.Contains("data-change-email-link", html);
        Assert.Contains("href=\"/Account/ChangeEmail\"", html);
    }

    [Fact]
    public async Task FullFlow_ChangesTheLogin_SignsOutTheOldSession_AndWarnsTheOldAddress()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        const string oldEmail = "troca-fluxo@example.com";
        const string newEmail = "troca-fluxo-novo@example.com";
        var session = await PageClient.LoggedInAsync(factory, oldEmail);

        var asked = await AskAsync(session, newEmail);
        Assert.Equal(HttpStatusCode.Redirect, asked.StatusCode);
        Assert.Contains("Enviamos um link para o e-mail novo", WebUtility.HtmlDecode(await session.GetStringAsync("/Account/Delete")));
        Assert.Equal(HttpStatusCode.Redirect, await LoginAsync(factory, oldEmail));

        var confirmPath = await ConfirmPathAsync(factory, sender, newEmail);
        var visitor = PageClient.Anonymous(factory);
        var page = await visitor.GetAsync(confirmPath);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("no-referrer", page.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("data-confirm-email-change", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Redirect, await LoginAsync(factory, oldEmail));

        var confirmed = await ConfirmAsync(visitor, confirmPath);
        Assert.Equal(HttpStatusCode.Redirect, confirmed.StatusCode);
        Assert.Equal("/Account/Login", confirmed.Headers.Location?.OriginalString);
        Assert.Contains("E-mail trocado. Entre com o e-mail novo.", WebUtility.HtmlDecode(await visitor.GetStringAsync("/Account/Login")));

        Assert.Equal(HttpStatusCode.Redirect, await LoginAsync(factory, newEmail));
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(factory, oldEmail));

        var old = await session.GetAsync("/Account/Delete");
        Assert.Equal(HttpStatusCode.Redirect, old.StatusCode);
        Assert.Contains("/Account/Login", old.Headers.Location!.OriginalString);

        await DispatchAsync(factory);
        Assert.Single(sender.Sent, m => m.To == oldEmail && m.Subject == "O e-mail da sua conta foi trocado");
    }

    [Fact]
    public async Task WrongPassword_ShowsTheErrorOnThePage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var client = await PageClient.LoggedInAsync(factory, "troca-senha-errada@example.com");

        var response = await AskAsync(client, "troca-senha-errada-novo@example.com", PageClient.Password + "x");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Senha atual incorreta.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task TheSameEmail_ShowsTheErrorOnThePage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var client = await PageClient.LoggedInAsync(factory, "troca-mesmo@example.com");

        var response = await AskAsync(client, "TROCA-MESMO@example.com");

        Assert.Contains("Esse já é o e-mail da sua conta.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Anonymous_CannotOpenTheChangePage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());

        var response = await PageClient.Anonymous(factory).GetAsync("/Account/ChangeEmail");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task ConfirmPage_WithoutTheLinkParts_OrWithAForgedToken_ShowsTheFailure()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var visitor = PageClient.Anonymous(factory);
        var forged = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));

        Assert.Contains("data-email-change-failed", await visitor.GetStringAsync("/Account/ConfirmEmailChange"));

        var response = await ConfirmAsync(visitor, $"/Account/ConfirmEmailChange?userId=qualquer&token={forged}");
        Assert.Contains("data-email-change-failed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InEnglish_TheChangePageIsInEnglish()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var client = await PageClient.LoggedInAsync(factory, "troca-ingles@example.com");
        client.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den-US");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Account/ChangeEmail"));

        Assert.Contains("Change e-mail", html);
        Assert.Contains("New e-mail", html);
    }
}
