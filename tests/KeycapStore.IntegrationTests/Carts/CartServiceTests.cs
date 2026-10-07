using KeycapStore.Application.Carts;
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
}
