using KeycapStore.Application.Catalog;
using KeycapStore.Application.Identity;
using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Catalog;

public class StockAlertTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<Guid> SaveProductAsync(WebApplicationFactory<Program> factory, int stock, bool active = true)
    {
        var product = new Product($"Kit Aviso {Guid.NewGuid():N}"[..20], 199.90m, stock, TestPackage.Standard);
        if (!active)
        {
            product.Deactivate();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task SetStockAsync(WebApplicationFactory<Program> factory, Guid productId, int stock)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, stock));
    }

    private static async Task<StockAlertResult> SubscribeAsync(WebApplicationFactory<Program> factory, string customerId, Guid productId, string language = "pt-BR")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStockAlertService>().SubscribeAsync(customerId, productId, language);
    }

    private static async Task<bool> IsSubscribedAsync(WebApplicationFactory<Program> factory, string customerId, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStockAlertService>().IsSubscribedAsync(customerId, productId);
    }

    private static async Task<int> NotifyAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IStockAlertNotifier>().NotifyAsync();
    }

    private static async Task<int> BackInStockEmailsAsync(WebApplicationFactory<Program> factory, string customerId, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxEmails.CountAsync(e => e.Kind == EmailKind.BackInStock && e.CustomerId == customerId && e.ProductId == productId);
    }

    [Fact]
    public async Task Subscribe_SoldOutKit_IsAccepted_AndAskingTwiceKeepsOne()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (customerId, _) = await CreateCustomerAsync(factory);
        var productId = await SaveProductAsync(factory, stock: 0);

        Assert.Equal(StockAlertResult.Subscribed, await SubscribeAsync(factory, customerId, productId));
        Assert.Equal(StockAlertResult.AlreadySubscribed, await SubscribeAsync(factory, customerId, productId));
        Assert.True(await IsSubscribedAsync(factory, customerId, productId));
    }

    [Fact]
    public async Task Subscribe_KitInStock_Inactive_OrPreOrder_IsNotAllowed()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (customerId, _) = await CreateCustomerAsync(factory);
        var inStock = await SaveProductAsync(factory, stock: 3);
        var inactive = await SaveProductAsync(factory, stock: 0, active: false);
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Aviso Pré-venda", slots: 1, minimum: 1);
        await SetStockAsync(factory, preOrder, 0);

        Assert.Equal(StockAlertResult.NotAllowed, await SubscribeAsync(factory, customerId, inStock));
        Assert.Equal(StockAlertResult.NotAllowed, await SubscribeAsync(factory, customerId, inactive));
        Assert.Equal(StockAlertResult.NotAllowed, await SubscribeAsync(factory, customerId, preOrder));
        Assert.False(await IsSubscribedAsync(factory, customerId, inStock));
    }

    [Fact]
    public async Task Subscribe_UnknownKit_Throws()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (customerId, _) = await CreateCustomerAsync(factory);

        await Assert.ThrowsAsync<ProductNotFoundException>(() => SubscribeAsync(factory, customerId, Guid.NewGuid()));
    }

    [Fact]
    public async Task Notify_WhileStillSoldOut_DoesNothing_ThenWhenItComesBack_QueuesOneEmailAndForgetsTheAlert()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (customerId, _) = await CreateCustomerAsync(factory);
        var productId = await SaveProductAsync(factory, stock: 0);
        await SubscribeAsync(factory, customerId, productId);

        await NotifyAsync(factory);
        Assert.Equal(0, await BackInStockEmailsAsync(factory, customerId, productId));
        Assert.True(await IsSubscribedAsync(factory, customerId, productId));

        await SetStockAsync(factory, productId, 2);
        await NotifyAsync(factory);
        await NotifyAsync(factory);

        Assert.Equal(1, await BackInStockEmailsAsync(factory, customerId, productId));
        Assert.False(await IsSubscribedAsync(factory, customerId, productId));
    }

    [Fact]
    public async Task Notify_KitBackButInactive_KeepsWaiting()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (customerId, _) = await CreateCustomerAsync(factory);
        var productId = await SaveProductAsync(factory, stock: 0);
        await SubscribeAsync(factory, customerId, productId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Products.Where(p => p.Id == productId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, 5).SetProperty(p => p.IsActive, false));
        }

        await NotifyAsync(factory);

        Assert.Equal(0, await BackInStockEmailsAsync(factory, customerId, productId));
        Assert.True(await IsSubscribedAsync(factory, customerId, productId));
    }

    [Fact]
    public async Task Notify_TwoRoundsAtTheSameTime_StillOneEmailPerAlert()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var productId = await SaveProductAsync(factory, stock: 0);
        var customers = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var (customerId, _) = await CreateCustomerAsync(factory);
            await SubscribeAsync(factory, customerId, productId);
            customers.Add(customerId);
        }

        await SetStockAsync(factory, productId, 1);
        await Task.WhenAll(NotifyAsync(factory), NotifyAsync(factory), NotifyAsync(factory));
        await NotifyAsync(factory);

        foreach (var customerId in customers)
        {
            Assert.Equal(1, await BackInStockEmailsAsync(factory, customerId, productId));
        }
    }

    [Fact]
    public async Task TheEmail_HasTheKitAPriceAndALinkToItsPage_InTheLanguageOfTheRequest()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var (customerId, address) = await CreateCustomerAsync(factory);
        var productId = await SaveProductAsync(factory, stock: 0);
        await SubscribeAsync(factory, customerId, productId, "en-US");

        await SetStockAsync(factory, productId, 1);
        await NotifyAsync(factory);
        await DispatchAsync(factory);

        var message = Assert.Single(sender.Sent, m => m.To == address);
        Assert.EndsWith("is back in stock", message.Subject);
        Assert.Contains("R$199.90", message.TextBody);
        Assert.Contains($"https://localhost:7290/Catalog/{productId}", message.TextBody);
    }

    [Fact]
    public async Task DeletingTheAccount_RemovesItsAlerts_AndTheExportListsThem()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync(new FakeEmailSender());
        var (customerId, email) = await CreateCustomerAsync(factory);
        var productId = await SaveProductAsync(factory, stock: 0);
        await SubscribeAsync(factory, customerId, productId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var data = await scope.ServiceProvider.GetRequiredService<IPersonalDataExport>().ExportAsync(customerId);
            Assert.Single(data.StockAlerts);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<IAccountDeletionService>().DeleteAsync(customerId, email);
            Assert.Equal(AccountDeletionResult.Deleted, result);
        }

        Assert.False(await IsSubscribedAsync(factory, customerId, productId));
    }
}
