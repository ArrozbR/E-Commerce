using KeycapStore.Domain.Carts;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Carts;

public class CartPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    // Dois kits do seed (migration SeedCatalog).
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");
    private static readonly Guid KitZefiro = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05");

    [Fact]
    public async Task Cart_WithItems_CanBeSavedAndReadBack()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var cart = new Cart("customer-persist");
        cart.AddItem(KitAurora, 2);
        cart.AddItem(KitZefiro, 1);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var saved = await db.Carts.SingleAsync(c => c.CustomerId == "customer-persist");

            Assert.Equal(2, saved.Items.Count);
            Assert.Equal(2, saved.Items.Single(i => i.ProductId == KitAurora).Quantity);
            Assert.Equal(1, saved.Items.Single(i => i.ProductId == KitZefiro).Quantity);
        }
    }

    [Fact]
    public async Task Cart_SecondCartForSameCustomer_IsRejectedByDatabase()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Carts.Add(new Cart("customer-unique"));
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Carts.Add(new Cart("customer-unique"));

            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
    }

    [Fact]
    public async Task Cart_WithUnknownProduct_IsRejectedByDatabase()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var cart = new Cart("customer-unknown-product");
        cart.AddItem(Guid.NewGuid(), 1);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Carts.Add(cart);

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
