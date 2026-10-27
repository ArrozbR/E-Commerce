using KeycapStore.Application.Payments;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Payments;

public class PaymentReconcilerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private const long OrderTotalInCents = 21500;

    private static async Task<(Guid OrderId, Guid ProductId, string SessionId)> SaveOrderAsync(
        WebApplicationFactory<Program> factory, string name, TimeSpan age, Action<Order>? advance = null, bool withSession = true)
    {
        var product = new Product($"Kit Conciliação {name}", 100m, 3);
        var order = new Order($"customer-{name}", DateTimeOffset.UtcNow - age, [new OrderItem(product.Id, product.Name, 100m, 2)], Curitiba);
        var sessionId = $"cs_test_{name}";
        if (withSession)
        {
            order.AttachStripeSession(sessionId);
        }

        advance?.Invoke(order);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (order.Id, product.Id, sessionId);
    }

    private static async Task<int> ReconcileAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPaymentReconciler>().ReconcileAsync();
    }

    private static async Task<(OrderStatus Status, int Stock)> StateAsync(WebApplicationFactory<Program> factory, Guid orderId, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var status = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
        var stock = await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync();
        return (status, stock);
    }

    private static CheckoutSessionState Paid(long amount = OrderTotalInCents, string currency = "brl") =>
        new("complete", "paid", amount, currency, "pi_test_conciliacao");

    private static readonly CheckoutSessionState Expired = new("expired", "unpaid", OrderTotalInCents, "brl", null);

    [Fact]
    public async Task StuckOrder_WithAPaidSession_BecomesPaid()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "pago", TimeSpan.FromMinutes(50));
        stripe.Sessions[sessionId] = Paid();

        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task OrderAwaitingConfirmation_WithAPaidSession_BecomesPaid()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "confirmando", TimeSpan.FromMinutes(50), o => o.MarkAwaitingConfirmation());
        stripe.Sessions[sessionId] = Paid();

        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }

    [Theory]
    [InlineData(2150, "brl")]
    [InlineData(OrderTotalInCents, "usd")]
    public async Task PaidSession_WithAnotherAmountOrCurrency_IsNotConfirmed(long amount, string currency)
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, $"valor-{amount}-{currency}", TimeSpan.FromMinutes(50));
        stripe.Sessions[sessionId] = Paid(amount, currency);

        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.AwaitingPayment, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task StuckOrder_WithAnExpiredSession_IsExpired_AndTheStockReturns()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "expirado", TimeSpan.FromMinutes(50));
        stripe.Sessions[sessionId] = Expired;

        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.Expired, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task ReconcilingTwice_ReturnsTheStockOnce()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "duas-vezes", TimeSpan.FromMinutes(50));
        stripe.Sessions[sessionId] = Expired;

        await ReconcileAsync(factory);
        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.Expired, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task OpenSession_ChangesNothing()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "aberto", TimeSpan.FromMinutes(50));
        stripe.Sessions[sessionId] = new CheckoutSessionState("open", "unpaid", OrderTotalInCents, "brl", null);

        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.AwaitingPayment, 3), await StateAsync(factory, orderId, productId));
        Assert.Contains(sessionId, stripe.SessionLookups);
    }

    [Fact]
    public async Task RecentOrder_IsNotChecked()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "recente", TimeSpan.FromMinutes(30));
        stripe.Sessions[sessionId] = Paid();

        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.AwaitingPayment, 3), await StateAsync(factory, orderId, productId));
        Assert.DoesNotContain(sessionId, stripe.SessionLookups);
    }

    [Fact]
    public async Task OrderWithoutSession_IsLeftForTheOrphanJob()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "sem-sessao", TimeSpan.FromMinutes(50), withSession: false);

        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.AwaitingPayment, 3), await StateAsync(factory, orderId, productId));
        Assert.DoesNotContain(sessionId, stripe.SessionLookups);
    }

    [Fact]
    public async Task OrderAlreadyPaid_IsNotChecked()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "ja-pago", TimeSpan.FromMinutes(50), o => o.ConfirmPayment("pi_test_ja_pago"));
        stripe.Sessions[sessionId] = Expired;

        await ReconcileAsync(factory);

        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
        Assert.DoesNotContain(sessionId, stripe.SessionLookups);
    }

    [Fact]
    public async Task StripeDown_LeavesTheOrderForTheNextRound()
    {
        var stripe = new FakePaymentGateway { FailSessionLookups = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId, sessionId) = await SaveOrderAsync(factory, "stripe-fora", TimeSpan.FromMinutes(50));
        stripe.Sessions[sessionId] = Paid();

        await ReconcileAsync(factory);
        Assert.Equal((OrderStatus.AwaitingPayment, 3), await StateAsync(factory, orderId, productId));

        stripe.FailSessionLookups = false;
        await ReconcileAsync(factory);
        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }
}
