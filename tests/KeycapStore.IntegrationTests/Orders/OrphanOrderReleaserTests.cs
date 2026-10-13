using KeycapStore.Application.Orders;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class OrphanOrderReleaserTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<(Guid OrderId, Guid ProductId)> CreateOrderAsync(
        WebApplicationFactory<Program> factory, string customerId, TimeSpan age, string? stripeSessionId = null)
    {
        var product = new Product($"Kit Órfão {customerId}", 100m, 3);
        var order = new Order(customerId, DateTimeOffset.UtcNow - age, [new OrderItem(product.Id, product.Name, product.Price, 2)], Curitiba);
        if (stripeSessionId is not null)
        {
            order.AttachStripeSession(stripeSessionId);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (order.Id, product.Id);
    }

    private static async Task ReleaseAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IOrphanOrderReleaser>().ReleaseOrphansAsync();
    }

    private static async Task<(OrderStatus Status, int Stock)> StateAsync(WebApplicationFactory<Program> factory, Guid orderId, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var status = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
        var stock = await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync();
        return (status, stock);
    }

    [Fact]
    public async Task OrderWithoutSession_OlderThan10Minutes_FailsAndReleasesTheStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await CreateOrderAsync(factory, "customer-orphan-old", TimeSpan.FromMinutes(11));

        await ReleaseAsync(factory);

        Assert.Equal((OrderStatus.Failed, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task OrderWithoutSession_YoungerThan10Minutes_IsKept()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await CreateOrderAsync(factory, "customer-orphan-young", TimeSpan.FromMinutes(9));

        await ReleaseAsync(factory);

        Assert.Equal((OrderStatus.AwaitingPayment, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task OrderWithSession_IsLeftForStripe()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await CreateOrderAsync(factory, "customer-orphan-session", TimeSpan.FromMinutes(11), "cs_test_orphan");

        await ReleaseAsync(factory);

        Assert.Equal((OrderStatus.AwaitingPayment, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task TwoJobsAtTheSameTime_ReleaseTheStockOnce()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await CreateOrderAsync(factory, "customer-orphan-parallel", TimeSpan.FromMinutes(11));

        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => ReleaseAsync(factory)));

        Assert.Equal((OrderStatus.Failed, 5), await StateAsync(factory, orderId, productId));
    }
}
