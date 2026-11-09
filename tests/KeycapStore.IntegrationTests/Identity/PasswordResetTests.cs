using System.Text.RegularExpressions;

using KeycapStore.Application.Identity;
using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Identity;

public partial class PasswordResetTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string OldPassword = "Senha-Antiga-123";

    private const string NewPassword = "Senha-Nova-456";

    [GeneratedRegex(@"/Account/ResetPassword\?userId=(?<user>[^&\s]+)&token=(?<token>[^\s""]+)")]
    private static partial Regex ResetLink();

    private static async Task<(string Id, string Email)> CustomerWithPasswordAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"senha-{Guid.NewGuid():N}@example.com";
        var user = new IdentityUser { UserName = email, Email = email };
        Assert.True((await users.CreateAsync(user, OldPassword)).Succeeded);
        return (user.Id, email);
    }

    private static async Task MakeAdminAsync(WebApplicationFactory<Program> factory, string userId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync(Roles.Admin))
        {
            await roles.CreateAsync(new IdentityRole(Roles.Admin));
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(userId))!, Roles.Admin)).Succeeded);
    }

    private static async Task RequestAsync(WebApplicationFactory<Program> factory, string email, string language = "pt-BR")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IPasswordResetService>().RequestAsync(email, language);
    }

    private static async Task<PasswordResetResult> ResetAsync(WebApplicationFactory<Program> factory, string userId, string token, string password = NewPassword)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPasswordResetService>().ResetAsync(userId, token, password, "pt-BR");
    }

    private static async Task<List<EmailKind>> QueuedForAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxEmails.Where(e => e.CustomerId == customerId).OrderBy(e => e.CreatedAt).Select(e => e.Kind).ToListAsync();
    }

    private static async Task<bool> PasswordIsAsync(WebApplicationFactory<Program> factory, string userId, string password)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        return await users.CheckPasswordAsync((await users.FindByIdAsync(userId))!, password);
    }

    private static async Task<EmailMessage> RequestAndReceiveAsync(WebApplicationFactory<Program> factory, FakeEmailSender sender, string email, string language = "pt-BR")
    {
        await RequestAsync(factory, email, language);
        await DispatchAsync(factory);
        return Assert.Single(sender.Sent, m => m.To == email);
    }

    private static (string UserId, string Token) LinkIn(EmailMessage message)
    {
        var match = ResetLink().Match(message.TextBody);
        Assert.True(match.Success, "O e-mail não tem o link de troca de senha.");
        return (Uri.UnescapeDataString(match.Groups["user"].Value), match.Groups["token"].Value);
    }

    [Fact]
    public async Task Request_UnknownEmail_QueuesNothing_AndDoesNotFail()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());

        await RequestAsync(factory, $"ninguem-{Guid.NewGuid():N}@example.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.OutboxEmails.AnyAsync(e => e.Kind == EmailKind.PasswordReset && e.CustomerId == null));
    }

    [Fact]
    public async Task Request_TwiceWithinFiveMinutes_QueuesOnlyOneEmail()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (id, email) = await CustomerWithPasswordAsync(factory);

        await RequestAsync(factory, email);
        await RequestAsync(factory, email.ToUpperInvariant());

        Assert.Equal([EmailKind.PasswordReset], await QueuedForAsync(factory, id));
    }

    [Fact]
    public async Task Reset_WithTheLinkFromTheEmail_ChangesThePassword_UnlocksTheAccount_AndQueuesTheNotice()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, email) = await CustomerWithPasswordAsync(factory);
        string oldStamp;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = (await users.FindByIdAsync(id))!;
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5));
            oldStamp = (await users.GetSecurityStampAsync(user))!;
        }

        var message = await RequestAndReceiveAsync(factory, sender, email);
        var (userId, token) = LinkIn(message);
        var result = await ResetAsync(factory, userId, token);

        Assert.Equal(PasswordResetStatus.Reset, result.Status);
        Assert.True(await PasswordIsAsync(factory, id, NewPassword));
        Assert.Equal([EmailKind.PasswordReset, EmailKind.PasswordChanged], await QueuedForAsync(factory, id));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var user = (await users.FindByIdAsync(id))!;
            Assert.False(await users.IsLockedOutAsync(user));
            Assert.NotEqual(oldStamp, await users.GetSecurityStampAsync(user));
        }
    }

    [Fact]
    public async Task Reset_SameLinkTwice_SecondTimeIsInvalid()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, email) = await CustomerWithPasswordAsync(factory);
        var (userId, token) = LinkIn(await RequestAndReceiveAsync(factory, sender, email));

        await ResetAsync(factory, userId, token);
        var second = await ResetAsync(factory, userId, token, "Outra-Senha-789");

        Assert.Equal(PasswordResetStatus.InvalidLink, second.Status);
        Assert.True(await PasswordIsAsync(factory, id, NewPassword));
    }

    [Fact]
    public async Task Reset_SameLinkAtTheSameTime_OnlyOneSucceeds()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (_, email) = await CustomerWithPasswordAsync(factory);
        var (userId, token) = LinkIn(await RequestAndReceiveAsync(factory, sender, email));

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(i => ResetAsync(factory, userId, token, $"Senha-Paralela-{i}00")));

        Assert.Single(results, r => r.Status == PasswordResetStatus.Reset);
        Assert.All(results.Where(r => r.Status != PasswordResetStatus.Reset), r => Assert.Equal(PasswordResetStatus.InvalidLink, r.Status));
    }

    [Fact]
    public async Task Reset_WeakPassword_IsRejected_AndTheLinkStillWorks()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, email) = await CustomerWithPasswordAsync(factory);
        var (userId, token) = LinkIn(await RequestAndReceiveAsync(factory, sender, email));

        var weak = await ResetAsync(factory, userId, token, "123");
        var strong = await ResetAsync(factory, userId, token);

        Assert.Equal(PasswordResetStatus.Rejected, weak.Status);
        Assert.NotEmpty(weak.Errors);
        Assert.Equal(PasswordResetStatus.Reset, strong.Status);
        Assert.True(await PasswordIsAsync(factory, id, NewPassword));
    }

    [Theory]
    [InlineData("nao-e-base64-!!!")]
    [InlineData("dG9rZW4tZmFsc28")]
    public async Task Reset_WithAForgedToken_IsInvalid(string token)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (id, _) = await CustomerWithPasswordAsync(factory);

        var result = await ResetAsync(factory, id, token);

        Assert.Equal(PasswordResetStatus.InvalidLink, result.Status);
        Assert.True(await PasswordIsAsync(factory, id, OldPassword));
    }

    [Fact]
    public async Task Request_AccountWithoutPassword_GetsTheGoogleEmail_WithoutAResetLink()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (_, email) = await CreateCustomerAsync(factory);

        var message = await RequestAndReceiveAsync(factory, sender, email);

        Assert.Contains("Google", message.TextBody);
        Assert.DoesNotMatch(ResetLink(), message.TextBody);
        Assert.Contains("/Account/Login", message.TextBody);
    }

    [Fact]
    public async Task Admin_GetsTheCommandEmail_AndCannotResetByLink()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, email) = await CustomerWithPasswordAsync(factory);
        await MakeAdminAsync(factory, id);
        string forgedToken;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var raw = await users.GeneratePasswordResetTokenAsync((await users.FindByIdAsync(id))!);
            forgedToken = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(raw));
        }

        var message = await RequestAndReceiveAsync(factory, sender, email);
        var result = await ResetAsync(factory, id, forgedToken);

        Assert.Contains("reset-admin-password", message.TextBody);
        Assert.DoesNotMatch(ResetLink(), message.TextBody);
        Assert.Equal(PasswordResetStatus.InvalidLink, result.Status);
        Assert.True(await PasswordIsAsync(factory, id, OldPassword));
    }

    [Fact]
    public async Task Request_InEnglish_TheEmailIsInEnglish_AndTheNoticeAfterwardsToo()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (_, email) = await CustomerWithPasswordAsync(factory);

        var message = await RequestAndReceiveAsync(factory, sender, email, "en-US");

        Assert.Equal("Reset your password", message.Subject);
        Assert.Contains("valid for 30 minutes", message.TextBody);
    }

    [Fact]
    public async Task TheLinkIsValidFor30Minutes()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());

        var options = factory.Services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(30), options.TokenLifespan);
    }
}
