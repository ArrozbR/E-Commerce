using KeycapStore.Application.Carts;
using KeycapStore.Domain.Carts;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Carts;

public class CartServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    [Fact]
    public async Task AddItem_FirstTime_CreatesCartWithServerPrices()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();

        await carts.AddItemAsync("customer-first", KitAurora, 2);
        var view = await carts.GetAsync("customer-first");

        var line = Assert.Single(view.Lines);
        Assert.Equal("Kit Aurora (base)", line.ProductName);
        Assert.Equal(349.90m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(699.80m, view.Total);
    }

    [Fact]
    public async Task AddItem_SameProductInTwoVisits_SumsInTheSameCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync("customer-twice", KitAurora, 2);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync("customer-twice", KitAurora, 1);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cart = await db.Carts.SingleAsync(c => c.CustomerId == "customer-twice");
            Assert.Equal(3, Assert.Single(cart.Items).Quantity);
        }
    }

    [Fact]
    public async Task ChangeQuantity_ReplacesTheQuantity()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync("customer-change", KitAurora, 3);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICartService>().ChangeQuantityAsync("customer-change", KitAurora, 1);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var view = await scope.ServiceProvider.GetRequiredService<ICartService>().GetAsync("customer-change");
            Assert.Equal(1, Assert.Single(view.Lines).Quantity);
        }
    }

    [Fact]
    public async Task AddItem_PastTheStock_IsRefused_AndKeepsTheQuantity()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var kit = new Product("Kit Três Unidades", 100m, 3, TestPackage.Standard);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(kit);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync("customer-stock-add", kit.Id, 3);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();

            var refused = await Assert.ThrowsAsync<CartStockLimitException>(() => carts.AddItemAsync("customer-stock-add", kit.Id, 1));

            Assert.Equal(3, refused.Available);
            Assert.Equal(3, Assert.Single((await carts.GetAsync("customer-stock-add")).Lines).Quantity);
        }
    }

    [Fact]
    public async Task ChangeQuantity_PastTheStock_IsRefused_ButLoweringIsAccepted()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var kit = new Product("Kit Estoque Baixo", 100m, 3, TestPackage.Standard);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(kit);
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync("customer-stock-change", kit.Id, 2);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var carts = scope.ServiceProvider.GetRequiredService<ICartService>();

            await Assert.ThrowsAsync<CartStockLimitException>(() => carts.ChangeQuantityAsync("customer-stock-change", kit.Id, 4));
            await carts.ChangeQuantityAsync("customer-stock-change", kit.Id, 3);

            var line = Assert.Single((await carts.GetAsync("customer-stock-change")).Lines);
            Assert.Equal(3, line.Quantity);
            Assert.Equal(3, line.AvailableStock);
        }
    }

    [Fact]
    public async Task RemoveItem_DeletesTheLineFromTheDatabase()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync("customer-remove", KitAurora, 2);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<ICartService>().RemoveItemAsync("customer-remove", KitAurora);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var view = await scope.ServiceProvider.GetRequiredService<ICartService>().GetAsync("customer-remove");
            Assert.Empty(view.Lines);
        }
    }


    [Fact]
    public async Task Get_WithoutCart_ReturnsEmpty()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();

        var view = await carts.GetAsync("customer-without-cart");

        Assert.Empty(view.Lines);
        Assert.Equal(0m, view.Total);
    }

    [Fact]
    public async Task AddItem_ProductTakenOffSale_IsTreatedAsNotFound()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = new KeycapStore.Domain.Catalog.Product("Kit Fora de Venda", 100m, 5, TestPackage.Standard);
        product.Deactivate();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();

        await Assert.ThrowsAsync<KeycapStore.Domain.Catalog.ProductNotFoundException>(() => carts.AddItemAsync("customer-off-sale", product.Id, 1));
    }

    [Fact]
    public async Task Get_ItemTakenOffSaleAfterwards_IsShownAsUnavailable()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = new KeycapStore.Domain.Catalog.Product("Kit Sai de Venda", 100m, 5, TestPackage.Standard);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        await carts.AddItemAsync("customer-unavailable", product.Id, 1);
        await db.Products.Where(p => p.Id == product.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.IsActive, false));

        var view = await carts.GetAsync("customer-unavailable");

        Assert.False(Assert.Single(view.Lines).IsAvailable);
        Assert.True(view.HasUnavailableItems);
    }

    [Fact]
    public async Task AddItem_PreOrderToACartWithOtherKits_IsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Carrinho 1");
        await using var scope = factory.Services.CreateAsyncScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
        await carts.AddItemAsync("customer-mix-1", KitAurora, 1);

        await Assert.ThrowsAsync<PreOrderMustBeAloneException>(() => carts.AddItemAsync("customer-mix-1", preOrder, 1));

        Assert.Equal("Kit Aurora (base)", Assert.Single((await carts.GetAsync("customer-mix-1")).Lines).ProductName);
    }

    [Fact]
    public async Task AddItem_KitToACartWithAPreOrder_IsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Carrinho 2");
        await using var scope = factory.Services.CreateAsyncScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
        await carts.AddItemAsync("customer-mix-2", preOrder, 1);

        await Assert.ThrowsAsync<PreOrderMustBeAloneException>(() => carts.AddItemAsync("customer-mix-2", KitAurora, 1));
    }

    [Fact]
    public async Task AddItem_SamePreOrderAgain_AddsToTheQuantity()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Carrinho 3");
        await using var scope = factory.Services.CreateAsyncScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
        await carts.AddItemAsync("customer-mix-3", preOrder, 1);

        await carts.AddItemAsync("customer-mix-3", preOrder, 1);

        Assert.Equal(2, Assert.Single((await carts.GetAsync("customer-mix-3")).Lines).Quantity);
    }

    [Fact]
    public async Task AddItem_PreOrderThatEnded_IsTreatedAsNotFound()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Pré-venda Encerrada");
        await TestPreOrders.EndCampaignAsync(factory, preOrder);
        await using var scope = factory.Services.CreateAsyncScope();
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();

        await Assert.ThrowsAsync<ProductNotFoundException>(() => carts.AddItemAsync("customer-ended", preOrder, 1));
    }
}
