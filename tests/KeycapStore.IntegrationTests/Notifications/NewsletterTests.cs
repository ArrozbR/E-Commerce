using KeycapStore.Application.Identity;
using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Notifications;

public class NewsletterTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<Guid> SaveProductAsync(WebApplicationFactory<Program> factory, int stock)
    {
        var product = new Product($"Kit Novo {Guid.NewGuid():N}"[..18], 259.90m, stock, TestPackage.Standard);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<string> SubscriberAsync(WebApplicationFactory<Program> factory, string language = "pt-BR")
    {
        var (customerId, _) = await CreateCustomerAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<INewsletterService>().SubscribeAsync(customerId, language);
        return customerId;
    }

    private static async Task AnnounceAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IProductAnnouncer>().AnnounceAsync();
    }

    private static async Task<List<EmailKind>> NewsForAsync(WebApplicationFactory<Program> factory, string customerId, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxEmails
            .Where(e => e.CustomerId == customerId && e.ProductId == productId && (e.Kind == EmailKind.NewKit || e.Kind == EmailKind.PreOrderOpened))
            .Select(e => e.Kind)
            .ToListAsync();
    }

    [Fact]
    public async Task Subscribe_Twice_KeepsOne_AndUnsubscribeTurnsItOff()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var customerId = await SubscriberAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var newsletter = scope.ServiceProvider.GetRequiredService<INewsletterService>();
        await newsletter.SubscribeAsync(customerId, "pt-BR");
        Assert.True(await newsletter.IsSubscribedAsync(customerId));

        await newsletter.UnsubscribeAsync(customerId);
        Assert.False(await newsletter.IsSubscribedAsync(customerId));
    }

    [Fact]
    public async Task NewKit_IsAnnouncedOnceToSubscribers_AndNotToOthers()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var subscriber = await SubscriberAsync(factory);
        var (outsider, _) = await CreateCustomerAsync(factory);
        var productId = await SaveProductAsync(factory, stock: 5);

        await AnnounceAsync(factory);
        await AnnounceAsync(factory);

        Assert.Equal([EmailKind.NewKit], await NewsForAsync(factory, subscriber, productId));
        Assert.Empty(await NewsForAsync(factory, outsider, productId));
    }

    [Fact]
    public async Task KitWithoutStock_WaitsUntilItHasStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var subscriber = await SubscriberAsync(factory);
        var productId = await SaveProductAsync(factory, stock: 0);

        await AnnounceAsync(factory);
        Assert.Empty(await NewsForAsync(factory, subscriber, productId));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 2));
        }

        await AnnounceAsync(factory);
        Assert.Equal([EmailKind.NewKit], await NewsForAsync(factory, subscriber, productId));
    }

    [Fact]
    public async Task OpenPreOrder_IsAnnouncedAsAPreOrder()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var subscriber = await SubscriberAsync(factory);
        var productId = await TestPreOrders.SaveOpenAsync(factory, "Kit Novo Lote", slots: 10, minimum: 2);

        await AnnounceAsync(factory);

        Assert.Equal([EmailKind.PreOrderOpened], await NewsForAsync(factory, subscriber, productId));
    }

    [Fact]
    public async Task ThreeRoundsAtTheSameTime_StillOneEmailPerSubscriber()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var subscribers = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            subscribers.Add(await SubscriberAsync(factory));
        }

        var productId = await SaveProductAsync(factory, stock: 3);

        await Task.WhenAll(AnnounceAsync(factory), AnnounceAsync(factory), AnnounceAsync(factory));

        foreach (var subscriber in subscribers)
        {
            Assert.Equal([EmailKind.NewKit], await NewsForAsync(factory, subscriber, productId));
        }
    }

    [Fact]
    public async Task TheEmail_HasTheKitAndALink_AndIsDroppedIfTheCustomerTurnedNewsOffBeforeIt_Left()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (stays, staysAddress) = await CreateCustomerAsync(factory);
        var (leaves, leavesAddress) = await CreateCustomerAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var newsletter = scope.ServiceProvider.GetRequiredService<INewsletterService>();
            await newsletter.SubscribeAsync(stays, "pt-BR");
            await newsletter.SubscribeAsync(leaves, "pt-BR");
        }

        var productId = await SaveProductAsync(factory, stock: 1);
        await AnnounceAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<INewsletterService>().UnsubscribeAsync(leaves);
        }

        await DispatchAsync(factory);

        var message = Assert.Single(sender.Sent, m => m.To == staysAddress);
        Assert.StartsWith("Kit novo na loja:", message.Subject);
        Assert.Contains($"https://localhost:7290/Catalog/{productId}", message.TextBody);
        Assert.Contains("ligou as novidades", message.TextBody);
        Assert.DoesNotContain(sender.Sent, m => m.To == leavesAddress);
    }

    [Fact]
    public async Task DeletingTheAccount_TurnsNewsOff_AndTheExportShowsTheConsentDate()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (customerId, email) = await CreateCustomerAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<INewsletterService>().SubscribeAsync(customerId, "pt-BR");
            var data = await scope.ServiceProvider.GetRequiredService<IPersonalDataExport>().ExportAsync(customerId);
            Assert.NotNull(data.NewsletterSubscribedAt);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.Equal(AccountDeletionResult.Deleted, await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(customerId, email));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<INewsletterService>().IsSubscribedAsync(customerId));
        }
    }
}
