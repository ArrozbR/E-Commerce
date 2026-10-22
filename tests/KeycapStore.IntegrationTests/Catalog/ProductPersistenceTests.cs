using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;

namespace KeycapStore.IntegrationTests.Catalog;

public class ProductPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(postgres.Container.GetConnectionString())
            .Options);

    [Fact]
    public async Task Product_CanBeSavedAndReadBack()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var product = new Product("Kit Aurora (base)", 349.90m, 40);

        await using (var db = CreateContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var saved = await db.Products.SingleAsync(p => p.Id == product.Id);

            Assert.Equal(product.Name, saved.Name);
            Assert.Equal(product.Price, saved.Price);
            Assert.Equal(product.Stock, saved.Stock);
        }
    }

    [Fact]
    public async Task SeededProducts_StayOnSale_AfterTheMigration()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var inactive = await db.Products.CountAsync(p => !p.IsActive);

        Assert.Equal(0, inactive);
    }

    [Fact]
    public async Task InactiveProduct_IsSavedAndReadBack()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var product = new Product("Kit Fora de Venda", 100m, 5);
        product.Deactivate();

        await using (var db = CreateContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var saved = await db.Products.SingleAsync(p => p.Id == product.Id);
            Assert.False(saved.IsActive);
        }
    }

    [Fact]
    public async Task EditingNameAndPrice_DoesNotOverwriteAStockChangedMeanwhile()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var product = new Product("Kit Edição Concorrente", 100m, 5);
        await using (var db = CreateContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        await using var admin = CreateContext();
        var editing = await admin.Products.SingleAsync(p => p.Id == product.Id);

        await using (var customer = CreateContext())
        {
            await customer.Products
                .Where(p => p.Id == product.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - 1));
        }

        editing.Update("Kit Edição Concorrente v2", 120m);
        await admin.SaveChangesAsync();

        await using (var db = CreateContext())
        {
            var saved = await db.Products.SingleAsync(p => p.Id == product.Id);
            Assert.Equal("Kit Edição Concorrente v2", saved.Name);
            Assert.Equal(120m, saved.Price);
            Assert.Equal(4, saved.Stock);
        }
    }
}
