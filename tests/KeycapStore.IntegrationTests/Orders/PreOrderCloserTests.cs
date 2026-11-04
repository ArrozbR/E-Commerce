using KeycapStore.Application.Orders;
using KeycapStore.Application.Payments;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class PreOrderCloserTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly TimeSpan TwoHours = TimeSpan.FromHours(2);

    private static async Task<int> CloseAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPreOrderCloser>().CloseDueCampaignsAsync();
    }

    private static async Task<Product> ProductAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }

    private static async Task<OrderStatus> StatusAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
    }

    private static async Task TakeSlotsAsync(WebApplicationFactory<Program> factory, Guid productId, int units)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - units));
    }

    [Fact]
    public async Task MinimumReached_ConfirmsTheBatch_AndKeepsTheOrdersPaid()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Lote Confirmado", slots: 50, minimum: 5);
        var first = await TestPreOrders.SaveOrderAsync(factory, preOrder, 3, o => o.ConfirmPayment());
        var second = await TestPreOrders.SaveOrderAsync(factory, preOrder, 2, o => o.ConfirmPayment());
        await TestPreOrders.EndCampaignAsync(factory, preOrder, TwoHours);

        Assert.Equal(1, await CloseAsync(factory));

        var product = await ProductAsync(factory, preOrder);
        Assert.Equal(PreOrderOutcome.Confirmed, product.PreOrderOutcome);
        Assert.False(product.IsActive);
        Assert.Equal(OrderStatus.Paid, await StatusAsync(factory, first));
        Assert.Equal(OrderStatus.Paid, await StatusAsync(factory, second));
    }

    [Fact]
    public async Task MinimumMissed_CancelsTheBatch_CancelsThePaidOrders_AndGivesTheSlotsBack()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Lote Cancelado", slots: 50, minimum: 20);
        await TakeSlotsAsync(factory, preOrder, 5);
        var first = await TestPreOrders.SaveOrderAsync(factory, preOrder, 3, o => o.ConfirmPayment("pi_test_lote_1"));
        var second = await TestPreOrders.SaveOrderAsync(factory, preOrder, 2, o => o.ConfirmPayment("pi_test_lote_2"));
        var expired = await TestPreOrders.SaveOrderAsync(factory, preOrder, 4, o => o.Expire());
        await TestPreOrders.EndCampaignAsync(factory, preOrder, TwoHours);

        Assert.Equal(1, await CloseAsync(factory));

        var product = await ProductAsync(factory, preOrder);
        Assert.Equal(PreOrderOutcome.Cancelled, product.PreOrderOutcome);
        Assert.False(product.IsActive);
        Assert.Equal(50, product.Stock);
        Assert.Equal(OrderStatus.Cancelled, await StatusAsync(factory, first));
        Assert.Equal(OrderStatus.Cancelled, await StatusAsync(factory, second));
        Assert.Equal(OrderStatus.Expired, await StatusAsync(factory, expired));
    }

    [Fact]
    public async Task MinimumMissed_TheRefundJobThenRefundsEveryOrder()
    {
        var payments = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(payments);
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Lote Reembolsado", slots: 50, minimum: 20);
        var order = await TestPreOrders.SaveOrderAsync(factory, preOrder, 1, o => o.ConfirmPayment("pi_test_lote_reembolso"));
        await TestPreOrders.EndCampaignAsync(factory, preOrder, TwoHours);
        await CloseAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRefundProcessor>().RefundPendingAsync();
        }

        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, order));
    }

    [Fact]
    public async Task CampaignStillOpen_OrEndedLessThanAnHourAgo_IsNotClosed()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var open = await TestPreOrders.SaveOpenAsync(factory, "Kit Ainda Aberto", minimum: 1);
        var justEnded = await TestPreOrders.SaveOpenAsync(factory, "Kit Acabou Agora", minimum: 1);
        await TestPreOrders.EndCampaignAsync(factory, justEnded, TimeSpan.FromMinutes(30));

        Assert.Equal(0, await CloseAsync(factory));

        Assert.Null((await ProductAsync(factory, open)).PreOrderOutcome);
        Assert.Null((await ProductAsync(factory, justEnded)).PreOrderOutcome);
    }

    [Fact]
    public async Task PendingPayment_HoldsTheClosing_UntilItIsResolved()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pix Aberto", slots: 50, minimum: 2);
        await TestPreOrders.SaveOrderAsync(factory, preOrder, 1, o => o.ConfirmPayment());
        var pending = await TestPreOrders.SaveOrderAsync(factory, preOrder, 1);
        await TestPreOrders.EndCampaignAsync(factory, preOrder, TwoHours);

        Assert.Equal(0, await CloseAsync(factory));
        Assert.Null((await ProductAsync(factory, preOrder)).PreOrderOutcome);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == pending);
            order.ConfirmPayment();
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await CloseAsync(factory));
        Assert.Equal(PreOrderOutcome.Confirmed, (await ProductAsync(factory, preOrder)).PreOrderOutcome);
    }

    [Fact]
    public async Task ClosedCampaign_IsNotClosedAgain()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Fechado Uma Vez", minimum: 1);
        await TestPreOrders.EndCampaignAsync(factory, preOrder, TwoHours);
        await CloseAsync(factory);

        Assert.Equal(0, await CloseAsync(factory));
    }

    [Fact]
    public async Task OrdersOfOtherProducts_AreNotTouched()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var failing = await TestPreOrders.SaveOpenAsync(factory, "Kit Lote Que Falha", minimum: 20);
        var other = await TestPreOrders.SaveOpenAsync(factory, "Kit Outro Lote Aberto", minimum: 1);
        var otherOrder = await TestPreOrders.SaveOrderAsync(factory, other, 1, o => o.ConfirmPayment());
        await TestPreOrders.EndCampaignAsync(factory, failing, TwoHours);

        await CloseAsync(factory);

        Assert.Equal(OrderStatus.Paid, await StatusAsync(factory, otherOrder));
        Assert.Null((await ProductAsync(factory, other)).PreOrderOutcome);
    }

    [Fact]
    public async Task CampaignReopenedByTheAdmin_IsNotClosed()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Reaberto", minimum: 20);
        await TestPreOrders.EndCampaignAsync(factory, preOrder, TwoHours);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<KeycapStore.Application.Catalog.IProductAdminService>()
                .ReschedulePreOrderAsync(preOrder, DateTimeOffset.UtcNow.AddDays(5), DateTimeOffset.UtcNow.AddDays(30));
        }

        Assert.Equal(0, await CloseAsync(factory));
        Assert.Null((await ProductAsync(factory, preOrder)).PreOrderOutcome);
    }
}
