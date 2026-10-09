using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class OrderPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");
    private static readonly Guid KitZefiro = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", "Sala 3", "Centro", "Curitiba", "PR", "80020-310");

    [Fact]
    public async Task Order_WithItemsAndAddress_CanBeSavedAndReadBack()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var order = new Order("customer-order", DateTimeOffset.UnixEpoch,
        [
            new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 2),
            new OrderItem(KitZefiro, "Kit Zéfiro (base)", 299.90m, 1),
        ], Curitiba);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Orders.SingleAsync(o => o.Id == order.Id);

            Assert.Equal(OrderStatus.AwaitingPayment, saved.Status);
            Assert.Equal(2, saved.Items.Count);
            Assert.Equal(349.90m, saved.Items.Single(i => i.ProductId == KitAurora).UnitPrice);
            Assert.Equal("Kit Zéfiro (base)", saved.Items.Single(i => i.ProductId == KitZefiro).ProductName);
            Assert.Equal("80020310", saved.ShippingAddress.PostalCode);
            Assert.Equal("Sala 3", saved.ShippingAddress.Complement);
            Assert.Equal(15.00m, saved.ShippingFee);
            Assert.Equal(1014.70m, saved.Total);
        }
    }

    [Fact]
    public async Task Order_KeepsItsPrice_WhenTheCatalogPriceChanges()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var order = new Order("customer-price", DateTimeOffset.UnixEpoch,
            [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Curitiba);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();

            await db.Products
                .Where(p => p.Id == KitAurora)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Price, 399.90m));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Orders.SingleAsync(o => o.Id == order.Id);

            Assert.Equal(349.90m, Assert.Single(saved.Items).UnitPrice);
        }
    }

    [Fact]
    public async Task Order_KeepsTheStripeSessionId()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var order = new Order("customer-session", DateTimeOffset.UnixEpoch,
            [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Curitiba);
        order.AttachStripeSession("cs_test_persistencia");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Orders.SingleAsync(o => o.Id == order.Id);

            Assert.Equal("cs_test_persistencia", saved.StripeSessionId);
        }
    }
}
