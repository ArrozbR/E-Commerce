using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class PreOrderOpenedAtMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task PreOrderCreatedBeforeTheMigration_GetsTheShipDateMinus80Days()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.Container.GetConnectionString())
            .Options;
        var productId = Guid.NewGuid();
        var shipsAt = new DateTimeOffset(2026, 12, 15, 3, 0, 0, TimeSpan.Zero);

        await using (var db = new AppDbContext(options))
        {
            await db.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync("AddProductPreOrderOutcome");
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO products ("Id", "Name", "Price", "Stock", "IsActive", "WeightKg", "WidthCm", "HeightCm", "LengthCm", "PreOrderEndsAt", "PreOrderShipsAt", "PreOrderMinimumUnits")
                VALUES ({productId}, 'Kit Pré-venda Antiga', 100, 5, true, 0.8, 30, 6, 20, {shipsAt.AddDays(-20)}, {shipsAt}, 3)
                """);
            await db.Database.MigrateAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

            Assert.Equal(shipsAt.AddDays(-80), product.PreOrderOpenedAt);
        }
    }
}
