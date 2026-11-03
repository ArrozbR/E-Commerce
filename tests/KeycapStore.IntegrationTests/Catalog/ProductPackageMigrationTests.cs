using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class ProductPackageMigrationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task ProductCreatedBeforeTheMigration_GetsTheStandardPackage()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.Container.GetConnectionString())
            .Options;
        var productId = Guid.NewGuid();

        await using (var db = new AppDbContext(options))
        {
            await db.GetInfrastructure().GetRequiredService<IMigrator>().MigrateAsync("AddOrderLanguage");
            await db.Database.ExecuteSqlAsync($"""INSERT INTO products ("Id", "Name", "Price", "Stock", "IsActive") VALUES ({productId}, 'Kit Criado Antes', 100, 1, true)""");
            await db.Database.MigrateAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var product = await db.Products.AsNoTracking().SingleAsync(p => p.Id == productId);

            Assert.Equal(new ProductPackage(0.8m, 30, 6, 20), product.Package);
        }
    }
}
