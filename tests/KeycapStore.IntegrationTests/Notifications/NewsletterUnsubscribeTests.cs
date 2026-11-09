using System.Net;
using System.Text.RegularExpressions;

using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Notifications;

public partial class NewsletterUnsubscribeTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [GeneratedRegex(@"https://localhost:7290(?<path>/Newsletter/Unsubscribe\?token=[^\s""]+)")]
    private static partial Regex UnsubscribeLink();

    private static async Task<(string CustomerId, string Path)> SubscriberWithLinkAsync(WebApplicationFactory<Program> factory, FakeEmailSender sender)
    {
        var (customerId, address) = await CreateCustomerAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<INewsletterService>().SubscribeAsync(customerId, "pt-BR");
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Product($"Kit Descadastro {Guid.NewGuid():N}"[..24], 79.90m, 2, TestPackage.Standard));
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<IProductAnnouncer>().AnnounceAsync();
        }

        await DispatchAsync(factory);
        var message = sender.Sent.First(m => m.To == address);
        Assert.NotNull(message.UnsubscribeLink);
        var match = UnsubscribeLink().Match(message.TextBody);
        Assert.True(match.Success, "O e-mail de novidades não tem o link de descadastro no texto.");
        Assert.Contains("Não quero mais receber estes e-mails", WebUtility.HtmlDecode(message.HtmlBody));
        return (customerId, match.Groups["path"].Value);
    }

    private static async Task<bool> SubscribedAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<INewsletterService>().IsSubscribedAsync(customerId);
    }

    [Fact]
    public async Task OpeningTheLink_OnlyShowsTheButton_AndConfirmingTurnsNewsOff()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, path) = await SubscriberWithLinkAsync(factory, sender);
        var client = PageClient.Anonymous(factory);

        var page = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("no-referrer", page.Headers.GetValues("Referrer-Policy").Single());
        Assert.Contains("data-unsubscribe", await page.Content.ReadAsStringAsync());
        Assert.True(await SubscribedAsync(factory, customerId));

        var token = System.Web.HttpUtility.ParseQueryString(new Uri("https://localhost" + path).Query)["token"]!;
        var confirmed = await client.PostAsync("/Newsletter/Unsubscribe", new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = token }));

        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Contains("data-unsubscribed", await confirmed.Content.ReadAsStringAsync());
        Assert.False(await SubscribedAsync(factory, customerId));
    }

    [Fact]
    public async Task OneClick_PostToTheLinkWithoutAntiforgery_TurnsNewsOff()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, path) = await SubscriberWithLinkAsync(factory, sender);

        var response = await PageClient.Anonymous(factory).PostAsync(path, new StringContent("List-Unsubscribe=One-Click", System.Text.Encoding.ASCII, "application/x-www-form-urlencoded"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await SubscribedAsync(factory, customerId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?token=")]
    [InlineData("?token=CfDJ8-inventado")]
    public async Task ForgedOrMissingToken_Is400_AndChangesNothing(string query)
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, _) = await SubscriberWithLinkAsync(factory, sender);
        var client = PageClient.Anonymous(factory);

        var page = await client.GetAsync("/Newsletter/Unsubscribe" + query);
        var post = await client.PostAsync("/Newsletter/Unsubscribe" + query, new StringContent("List-Unsubscribe=One-Click", System.Text.Encoding.ASCII, "application/x-www-form-urlencoded"));

        Assert.Equal(HttpStatusCode.BadRequest, page.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        Assert.Contains("data-unsubscribe-invalid", await post.Content.ReadAsStringAsync());
        Assert.True(await SubscribedAsync(factory, customerId));
    }

    [Fact]
    public async Task ATokenFromAnotherPurpose_DoesNotWork()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, _) = await SubscriberWithLinkAsync(factory, sender);
        var otherPurpose = factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("Outro.Proposito").Protect(customerId);

        var response = await PageClient.Anonymous(factory).PostAsync(
            "/Newsletter/Unsubscribe?token=" + Uri.EscapeDataString(otherPurpose),
            new StringContent("List-Unsubscribe=One-Click", System.Text.Encoding.ASCII, "application/x-www-form-urlencoded"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(await SubscribedAsync(factory, customerId));
    }

    [Fact]
    public async Task OrderEmails_HaveNoUnsubscribeLink()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, address) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId);
        await QueueAsync(factory, EmailKind.OrderPaid, orderId);

        await DispatchAsync(factory);

        var message = Assert.Single(sender.Sent, m => m.To == address);
        Assert.Null(message.UnsubscribeLink);
        Assert.DoesNotMatch(UnsubscribeLink(), message.TextBody);
    }
}
