using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Payments;

public class RefundProcessorTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static Order NewOrder() =>
        new("customer-refund", DateTimeOffset.UtcNow, [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Curitiba);

    private static async Task<Guid> SaveAsync(WebApplicationFactory<Program> factory, Order order)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order.Id;
    }

    private static Task<Guid> OutOfStockOrderAsync(WebApplicationFactory<Program> factory)
    {
        var order = NewOrder();
        order.Expire();
        order.MarkOutOfStock($"pi_test_{order.Id:N}");
        return SaveAsync(factory, order);
    }

    private static Task<Guid> CancelledOrderAsync(WebApplicationFactory<Program> factory, string? paymentIntentId)
    {
        var order = NewOrder();
        order.AttachStripeSession($"cs_test_{order.Id:N}");
        order.ConfirmPayment(paymentIntentId);
        order.Cancel();
        return SaveAsync(factory, order);
    }

    private static Task<Guid> PaidOrderAsync(WebApplicationFactory<Program> factory)
    {
        var order = NewOrder();
        order.ConfirmPayment("pi_test_paid");
        return SaveAsync(factory, order);
    }

    private static async Task RefundAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IRefundProcessor>().RefundPendingAsync();
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
        var orderId = await OutOfStockOrderAsync(factory);

        await RefundAsync(factory);

        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, orderId));
        Assert.Contains((orderId, $"pi_test_{orderId:N}"), stripe.Refunds);
    }

    [Fact]
    public async Task Cancelled_WithThePaymentIntentSaved_IsRefunded()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await CancelledOrderAsync(factory, "pi_test_saved");

        await RefundAsync(factory);

        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, orderId));
        Assert.Contains((orderId, "pi_test_saved"), stripe.Refunds);
    }

    [Fact]
    public async Task Cancelled_OldOrderWithoutThePaymentIntent_AsksStripeBySession()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await CancelledOrderAsync(factory, paymentIntentId: null);

        await RefundAsync(factory);

        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, orderId));
        Assert.Contains((orderId, $"pi_from_cs_test_{orderId:N}"), stripe.Refunds);
    }

    [Fact]
    public async Task RefundRefusedByStripe_KeepsWaiting_AndIsRetriedNextRound()
    {
        var stripe = new FakePaymentGateway { FailRefunds = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await CancelledOrderAsync(factory, "pi_test_retry");

        await RefundAsync(factory);
        Assert.Equal(OrderStatus.Cancelled, await StatusAsync(factory, orderId));

        stripe.FailRefunds = false;
        await RefundAsync(factory);
        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, orderId));
    }

    [Fact]
    public async Task PaidOrder_IsNotRefunded()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await PaidOrderAsync(factory);

        await RefundAsync(factory);

        Assert.Equal(OrderStatus.Paid, await StatusAsync(factory, orderId));
        Assert.DoesNotContain(stripe.Refunds, r => r.OrderId == orderId);
    }

    [Fact]
    public async Task FiveRoundsAtTheSameTime_EndRefunded_WithoutErrors()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var orderId = await CancelledOrderAsync(factory, "pi_test_parallel");

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => RefundAsync(factory)));

        Assert.Equal(OrderStatus.Refunded, await StatusAsync(factory, orderId));
    }
}
