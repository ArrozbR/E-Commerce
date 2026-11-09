using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Identity;

public partial class PasswordResetPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string NewPassword = "Senha#Nova456";

    [GeneratedRegex(@"https://localhost:7290(?<path>/Account/ResetPassword\?userId=[^&\s]+&token=[^\s""]+)")]
    private static partial Regex ResetLink();

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string path, string formPage, Dictionary<string, string> fields)
    {
        fields["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync(formPage));
        return await client.PostAsync(path, new FormUrlEncodedContent(fields));
    }

    private static Task<HttpResponseMessage> AskForLinkAsync(HttpClient client, string email) =>
        PostAsync(client, "/Account/ForgotPassword", "/Account/ForgotPassword", new() { ["Email"] = email });

    private static async Task<string> ResetPathFromEmailAsync(WebApplicationFactory<Program> factory, FakeEmailSender sender, string email)
    {
        await DispatchAsync(factory);
        var message = Assert.Single(sender.Sent, m => m.To == email);
        var match = ResetLink().Match(message.TextBody);
        Assert.True(match.Success, "O e-mail não tem o link de troca de senha.");
        return match.Groups["path"].Value;
    }

    private static Task<HttpResponseMessage> ChooseAsync(HttpClient client, string resetPath, string password, string? confirm = null)
    {
        var query = System.Web.HttpUtility.ParseQueryString(new Uri("https://localhost" + resetPath).Query);
        return PostAsync(client, "/Account/ResetPassword", resetPath, new()
        {
            ["UserId"] = query["userId"]!,
            ["Token"] = query["token"]!,
            ["NewPassword"] = password,
            ["ConfirmNewPassword"] = confirm ?? password,
        });
    }

    private static async Task<HttpStatusCode> LoginAsync(WebApplicationFactory<Program> factory, string email, string password)
    {
        var client = PageClient.Anonymous(factory);
        var response = await PostAsync(client, "/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = password });
        return response.StatusCode;
    }

    [Fact]
    public async Task LoginPage_LinksToForgotPassword()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());

        var html = await PageClient.Anonymous(factory).GetStringAsync("/Account/Login");

        Assert.Contains("href=\"/Account/ForgotPassword\"", html);
    }

    [Fact]
    public async Task ForgotPassword_KnownAndUnknownEmail_LookExactlyTheSame()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        await PageClient.LoggedInAsync(factory, "pagina-senha-conhecida@example.com");

        var known = PageClient.Anonymous(factory);
        var unknown = PageClient.Anonymous(factory);
        var knownResponse = await AskForLinkAsync(known, "pagina-senha-conhecida@example.com");
        var unknownResponse = await AskForLinkAsync(unknown, "ninguem-tem-esta-conta@example.com");

        Assert.Equal(HttpStatusCode.Redirect, knownResponse.StatusCode);
        Assert.Equal(knownResponse.StatusCode, unknownResponse.StatusCode);
        Assert.Equal(knownResponse.Headers.Location, unknownResponse.Headers.Location);

        var knownPage = await known.GetStringAsync(knownResponse.Headers.Location);
        var unknownPage = await unknown.GetStringAsync(unknownResponse.Headers.Location);
        Assert.Contains("data-forgot-sent", knownPage);
        Assert.Equal(WithoutTokens(knownPage), WithoutTokens(unknownPage));
    }

    private static string WithoutTokens(string html) =>
        Regex.Replace(html, @"value=""[^""]*""", "value=\"\"");

    [Fact]
    public async Task FullFlow_ChangesThePassword_SignsOutOtherSessions_AndAsksToSignInAgain()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        const string email = "pagina-senha-fluxo@example.com";
        var oldSession = await PageClient.LoggedInAsync(factory, email);
        var visitor = PageClient.Anonymous(factory);

        await AskForLinkAsync(visitor, email);
        var resetPath = await ResetPathFromEmailAsync(factory, sender, email);

        var form = await visitor.GetAsync(resetPath);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        Assert.Equal("no-referrer", form.Headers.GetValues("Referrer-Policy").Single());
        Assert.True(form.Headers.CacheControl?.NoStore);

        var chosen = await ChooseAsync(visitor, resetPath, NewPassword);
        Assert.Equal(HttpStatusCode.Redirect, chosen.StatusCode);
        Assert.Equal("/Account/Login", chosen.Headers.Location?.OriginalString);
        Assert.Contains("Senha trocada. Entre com a senha nova.", WebUtility.HtmlDecode(await visitor.GetStringAsync("/Account/Login")));

        Assert.Equal(HttpStatusCode.Redirect, await LoginAsync(factory, email, NewPassword));
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(factory, email, PageClient.Password));

        var stillSignedIn = await oldSession.GetAsync("/Account/Delete");
        Assert.Equal(HttpStatusCode.Redirect, stillSignedIn.StatusCode);
        Assert.Contains("/Account/Login", stillSignedIn.Headers.Location?.OriginalString);

        var reused = await ChooseAsync(PageClient.Anonymous(factory), resetPath, "Senha#Outra789");
        Assert.Contains("data-reset-invalid", await reused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResetPassword_WithoutTheLinkParts_ShowsTheInvalidLinkPage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());

        var html = await PageClient.Anonymous(factory).GetStringAsync("/Account/ResetPassword");

        Assert.Contains("data-reset-invalid", html);
        Assert.Contains("href=\"/Account/ForgotPassword\"", html);
    }

    [Fact]
    public async Task ResetPassword_WithAForgedToken_ShowsTheInvalidLinkPage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        await PageClient.LoggedInAsync(factory, "pagina-senha-falsa@example.com");

        var response = await ChooseAsync(PageClient.Anonymous(factory), "/Account/ResetPassword?userId=qualquer&token=dG9rZW4tZmFsc28", NewPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-reset-invalid", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResetPassword_ConfirmationDifferent_ShowsTheErrorAndKeepsTheForm()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        const string email = "pagina-senha-confere@example.com";
        await PageClient.LoggedInAsync(factory, email);
        var visitor = PageClient.Anonymous(factory);
        await AskForLinkAsync(visitor, email);
        var resetPath = await ResetPathFromEmailAsync(factory, sender, email);

        var response = await ChooseAsync(visitor, resetPath, NewPassword, "Senha#Diferente1");

        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("As senhas não conferem.", html);
        Assert.DoesNotContain("data-reset-invalid", html);
    }

    [Fact]
    public async Task ForgotPassword_InEnglish_ShowsEnglishAndSendsTheEmailInEnglish()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        const string email = "pagina-senha-ingles@example.com";
        await PageClient.LoggedInAsync(factory, email);
        var visitor = PageClient.Anonymous(factory);
        visitor.DefaultRequestHeaders.Add("Cookie", ".AspNetCore.Culture=c%3Den-US%7Cuic%3Den-US");

        var response = await AskForLinkAsync(visitor, email);
        var page = WebUtility.HtmlDecode(await visitor.GetStringAsync(response.Headers.Location));
        await DispatchAsync(factory);

        Assert.Contains("If there is an account with that e-mail", page);
        Assert.Equal("Reset your password", Assert.Single(sender.Sent, m => m.To == email).Subject);
    }

    [Fact]
    public async Task ForgotPassword_EmptyEmail_ShowsTheValidationError()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var visitor = PageClient.Anonymous(factory);

        var response = await AskForLinkAsync(visitor, "");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Informe o e-mail.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }
}
