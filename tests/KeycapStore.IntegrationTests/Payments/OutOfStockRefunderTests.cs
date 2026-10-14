using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Payments;

public class OutOfStockRefunderTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<Guid> CreateOrderAsync(WebApplicationFactory<Program> factory, bool outOfStock)
    {
        var order = new Order("customer-refund", DateTimeOffset.UtcNow, [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Curitiba);
        if (outOfStock)
        {
            order.Expire();
            order.MarkOutOfStock($"pi_test_{order.Id:N}");
        }
        else
        {
            order.ConfirmPayment();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order.Id;
    }

    private static async Task RefundAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOutOfStockRefunder>().RefundPendingAsync();
    }

    private static async Task<OrderStatus> StatusAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
    }

    [Fact]
    public async Task PaidOutOfStock_IsRefundedAtStripe_AndGoesToRefunded()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await CreateOrderAsync(factory, outOfStock: true);

        await RefundAsync(factory);

        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, orderId));
        Assert.Contains((orderId, $"pi_test_{orderId:N}"), stripe.Refunds);
    }

    [Fact]
    public async Task RefundRefusedByStripe_KeepsPaidOutOfStock_AndIsRetriedNextRound()
    {
        var stripe = new FakePaymentGateway { FailRefunds = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await CreateOrderAsync(factory, outOfStock: true);

        await RefundAsync(factory);
        Assert.Equal(OrderStatus.PaidOutOfStock, await StatusAsync(factory, orderId));

        stripe.FailRefunds = false;
        await RefundAsync(factory);
        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, orderId));
    }

    [Fact]
    public async Task PaidOrder_IsNotRefunded()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await CreateOrderAsync(factory, outOfStock: false);

        await RefundAsync(factory);

        Assert.Equal(OrderStatus.Paid, await StatusAsync(factory, orderId));
        Assert.DoesNotContain(stripe.Refunds, r => r.OrderId == orderId);
    }

    [Fact]
    public async Task FiveRoundsAtTheSameTime_EndRefunded_WithoutErrors()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await CreateOrderAsync(factory, outOfStock: true);

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => RefundAsync(factory)));

        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, orderId));
    }
}
