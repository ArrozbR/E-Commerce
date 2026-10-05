using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.IntegrationTests.Catalog;

public class CatalogQueriesTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.Container.GetConnectionString())
            .Options);

    [Fact]
    public async Task ListProductsAsync_ReturnsAllProductsOrderedByName()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
            db.Products.AddRange(
                new Product("Test Kit Zéfiro (base)", 299.90m, 10),
                new Product("Test Kit Aurora (base)", 349.90m, 0),
                new Product("Test Kit Maré (novelties)", 129.90m, 3));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var products = await new CatalogQueries(db).ListProductsAsync();

            var testProducts = products
                .Where(p => p.Name.StartsWith("Test", StringComparison.Ordinal))
                .ToList();

            Assert.Equal(
                new[] { "Test Kit Aurora (base)", "Test Kit Maré (novelties)", "Test Kit Zéfiro (base)" },
                testProducts.Select(p => p.Name));

            Assert.Equal(349.90m, testProducts[0].Price);
            Assert.Equal(0, testProducts[0].Stock);
        }
    }
}
