using System.Text;
using System.Text.RegularExpressions;

using KeycapStore.Application.Identity;
using KeycapStore.Application.Notifications;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Identity;

public partial class EmailChangeTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [GeneratedRegex(@"/Account/ConfirmEmailChange\?userId=(?<user>[^&\s]+)&token=(?<token>[^\s""]+)")]
    private static partial Regex ConfirmLink();

    private static string NewAddress() => $"novo-{Guid.NewGuid():N}@example.com";

    private static async Task<(string Id, string Email)> CustomerWithPasswordAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"troca-{Guid.NewGuid():N}@example.com";
        var user = new IdentityUser { UserName = email, Email = email };
        Assert.True((await users.CreateAsync(user, PageClient.Password)).Succeeded);
        return (user.Id, email);
    }

    private static async Task<EmailChangeRequestStatus> RequestAsync(WebApplicationFactory<Program> factory, string customerId, string newEmail, string? password = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEmailChangeService>()
            .RequestAsync(customerId, newEmail, password ?? PageClient.Password, "pt-BR");
    }

    private static async Task<EmailChangeConfirmStatus> ConfirmAsync(WebApplicationFactory<Program> factory, string customerId, string token)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IEmailChangeService>().ConfirmAsync(customerId, token, "pt-BR");
    }

    private static async Task<IdentityUser> UserAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByIdAsync(customerId))!;
    }

    private static async Task<string> TokenSentToAsync(WebApplicationFactory<Program> factory, FakeEmailSender sender, string address)
    {
        await DispatchAsync(factory);
        var message = Assert.Single(sender.Sent, m => m.To == address);
        var match = ConfirmLink().Match(message.TextBody);
        Assert.True(match.Success, "O e-mail não tem o link de confirmação.");
        return match.Groups["token"].Value;
    }

    private static async Task<int> QueuedAsync(WebApplicationFactory<Program> factory, string customerId, EmailKind kind)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxEmails.CountAsync(e => e.CustomerId == customerId && e.Kind == kind);
    }

    [Fact]
    public async Task Request_SendsTheLinkToTheNewAddress_AndTheAccountDoesNotChangeYet()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, email) = await CustomerWithPasswordAsync(factory);
        var target = NewAddress();

        Assert.Equal(EmailChangeRequestStatus.Requested, await RequestAsync(factory, id, target));
        await TokenSentToAsync(factory, sender, target);

        Assert.Equal(email, (await UserAsync(factory, id)).Email);
        Assert.DoesNotContain(sender.Sent, m => m.To == email);
    }

    [Fact]
    public async Task Request_WrongPassword_IsRefused_AndQueuesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (id, _) = await CustomerWithPasswordAsync(factory);

        var result = await RequestAsync(factory, id, NewAddress(), PageClient.Password + "x");

        Assert.Equal(EmailChangeRequestStatus.WrongPassword, result);
        Assert.Equal(0, await QueuedAsync(factory, id, EmailKind.EmailChangeConfirm));
    }

    [Fact]
    public async Task Request_TheSameEmailInAnotherCase_IsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (id, email) = await CustomerWithPasswordAsync(factory);

        Assert.Equal(EmailChangeRequestStatus.SameEmail, await RequestAsync(factory, id, email.ToUpperInvariant()));
    }

    [Fact]
    public async Task Request_GoogleOnlyAccountOrAdmin_IsNotAllowed()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (googleOnly, _) = await CreateCustomerAsync(factory);
        var (admin, _) = await CustomerWithPasswordAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(Roles.Admin))
            {
                await roles.CreateAsync(new IdentityRole(Roles.Admin));
            }

            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            await users.AddToRoleAsync((await users.FindByIdAsync(admin))!, Roles.Admin);
        }

        Assert.Equal(EmailChangeRequestStatus.NotAllowed, await RequestAsync(factory, googleOnly, NewAddress()));
        Assert.Equal(EmailChangeRequestStatus.NotAllowed, await RequestAsync(factory, admin, NewAddress()));
    }

    [Fact]
    public async Task Confirm_ChangesEmailAndLogin_SignsOutOtherSessions_AndWarnsTheOldAddress()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, oldEmail) = await CustomerWithPasswordAsync(factory);
        var oldStamp = (await UserAsync(factory, id)).SecurityStamp;
        var target = NewAddress();
        await RequestAsync(factory, id, target);
        var token = await TokenSentToAsync(factory, sender, target);

        Assert.Equal(EmailChangeConfirmStatus.Changed, await ConfirmAsync(factory, id, token));

        var user = await UserAsync(factory, id);
        Assert.Equal(target, user.Email);
        Assert.Equal(target, user.UserName);
        Assert.NotEqual(oldStamp, user.SecurityStamp);

        await DispatchAsync(factory);
        var notice = Assert.Single(sender.Sent, m => m.To == oldEmail);
        Assert.Equal("O e-mail da sua conta foi trocado", notice.Subject);
        Assert.Contains("@example.com", notice.TextBody);
        Assert.DoesNotContain(target, notice.TextBody);
    }

    [Fact]
    public async Task Confirm_TheSameLinkTwice_SecondTimeIsInvalid()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, _) = await CustomerWithPasswordAsync(factory);
        var target = NewAddress();
        await RequestAsync(factory, id, target);
        var token = await TokenSentToAsync(factory, sender, target);

        await ConfirmAsync(factory, id, token);

        Assert.Equal(EmailChangeConfirmStatus.InvalidLink, await ConfirmAsync(factory, id, token));
        Assert.Equal(1, await QueuedAsync(factory, id, EmailKind.EmailChanged));
    }

    [Fact]
    public async Task ANewerRequest_ReplacesTheOlderOne()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, _) = await CustomerWithPasswordAsync(factory);
        var first = NewAddress();
        var second = NewAddress();

        await RequestAsync(factory, id, first);
        await RequestAsync(factory, id, second);
        var token = await TokenSentToAsync(factory, sender, second);

        Assert.DoesNotContain(sender.Sent, m => m.To == first);
        Assert.Equal(EmailChangeConfirmStatus.Changed, await ConfirmAsync(factory, id, token));
        Assert.Equal(second, (await UserAsync(factory, id)).Email);
    }

    [Fact]
    public async Task Confirm_WhenTheNewEmailBelongsToAnotherAccount_IsRefused_AndNothingChanges()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (id, email) = await CustomerWithPasswordAsync(factory);
        var (_, taken) = await CustomerWithPasswordAsync(factory);

        Assert.Equal(EmailChangeRequestStatus.Requested, await RequestAsync(factory, id, taken));
        var token = await TokenSentToAsync(factory, sender, taken);

        Assert.Equal(EmailChangeConfirmStatus.EmailInUse, await ConfirmAsync(factory, id, token));
        Assert.Equal(email, (await UserAsync(factory, id)).Email);
        Assert.Equal(0, await QueuedAsync(factory, id, EmailKind.EmailChanged));
    }

    [Fact]
    public async Task Confirm_WithAForgedToken_IsInvalid()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (id, email) = await CustomerWithPasswordAsync(factory);
        await RequestAsync(factory, id, NewAddress());
        var forged = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));

        Assert.Equal(EmailChangeConfirmStatus.InvalidLink, await ConfirmAsync(factory, id, forged));
        Assert.Equal(email, (await UserAsync(factory, id)).Email);
    }
}
