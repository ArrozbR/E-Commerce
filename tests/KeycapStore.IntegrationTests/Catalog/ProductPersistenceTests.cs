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

        var product = new Product("Kit Aurora (base)", 349.90m, 40, TestPackage.Standard);

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
            Assert.Equal(product.Package, saved.Package);
        }
    }

    [Theory]
    [InlineData("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01", "0.8", 30, 6, 20)]
    [InlineData("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e02", "0.3", 20, 4, 12)]
    [InlineData("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e05", "0.8", 30, 6, 20)]
    [InlineData("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e06", "0.3", 20, 4, 12)]
    public async Task SeededKits_HaveTheirPackage(string id, string weight, int widthCm, int heightCm, int lengthCm)
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var kit = await db.Products.AsNoTracking().SingleAsync(p => p.Id == Guid.Parse(id));

        Assert.Equal(new ProductPackage(decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture), widthCm, heightCm, lengthCm), kit.Package);
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

        var product = new Product("Kit Fora de Venda", 100m, 5, TestPackage.Standard);
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

        var product = new Product("Kit Edição Concorrente", 100m, 5, TestPackage.Standard);
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

    [Fact]
    public async Task PreOrderProduct_IsSavedAndReadBack()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }

        var now = DateTimeOffset.UtcNow;
        var campaign = new PreOrder(now.AddDays(10), now.AddDays(40), 20);
        var product = Product.ForPreOrder("Kit Nebulosa (base)", 399.90m, 50, TestPackage.Standard, campaign, now);

        await using (var db = CreateContext())
        {
            db.Products.Add(product);
            await db.SaveChangesAsync();
        }

        await using (var db = CreateContext())
        {
            var saved = await db.Products.AsNoTracking().SingleAsync(p => p.Id == product.Id);

            Assert.Equal(campaign.EndsAt.ToUnixTimeMilliseconds(), saved.PreOrder!.EndsAt.ToUnixTimeMilliseconds());
            Assert.Equal(campaign.ShipsAt.ToUnixTimeMilliseconds(), saved.PreOrder.ShipsAt.ToUnixTimeMilliseconds());
            Assert.Equal(20, saved.PreOrder.MinimumUnits);
            Assert.Equal(50, saved.Stock);
        }
    }
}
