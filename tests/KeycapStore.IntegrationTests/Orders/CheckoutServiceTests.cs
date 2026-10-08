using KeycapStore.Application.Carts;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class CheckoutServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    // Cada teste cria os próprios produtos, para não depender do estoque do seed.
    private static async Task<Guid> NewProductAsync(WebApplicationFactory<Program> factory, string name, int stock)
    {
        var product = new Product(name, 100m, stock);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task AddToCartAsync(WebApplicationFactory<Program> factory, string customerId, Guid productId, int quantity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync(customerId, productId, quantity);
    }

    private static async Task<PlaceOrderResult> PlaceOrderAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceOrderAsync(customerId, Curitiba);
    }

    private static async Task<int> StockOfAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync();
    }

    [Fact]
    public async Task PlaceOrder_ReservesStock_SavesTheOrder_AndEmptiesTheCart()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var kit = await NewProductAsync(factory, "Kit Teste Sucesso", stock: 5);
        await AddToCartAsync(factory, "customer-success", kit, 2);

        var result = await PlaceOrderAsync(factory, "customer-success");

        Assert.True(result.Succeeded);
        Assert.Equal(3, await StockOfAsync(factory, kit));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == result.OrderId);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(2, Assert.Single(order.Items).Quantity);
        Assert.Equal(215.00m, order.Total);
        Assert.False(await db.Carts.AnyAsync(c => c.CustomerId == "customer-success"));
    }

    [Fact]
    public async Task PlaceOrder_WhenOneItemIsOutOfStock_ChangesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var available = await NewProductAsync(factory, "Kit Teste Disponível", stock: 5);
        var scarce = await NewProductAsync(factory, "Kit Teste Escasso", stock: 1);
        await AddToCartAsync(factory, "customer-rollback", available, 1);
        await AddToCartAsync(factory, "customer-rollback", scarce, 2);

        var result = await PlaceOrderAsync(factory, "customer-rollback");

        Assert.False(result.Succeeded);
        Assert.Contains("Kit Teste Escasso", result.Error);
        Assert.Equal(5, await StockOfAsync(factory, available));
        Assert.Equal(1, await StockOfAsync(factory, scarce));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Orders.AnyAsync(o => o.CustomerId == "customer-rollback"));
        Assert.Equal(2, (await db.Carts.SingleAsync(c => c.CustomerId == "customer-rollback")).Items.Count);
    }

    [Fact]
    public async Task PlaceOrder_WithEmptyCart_Fails()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var result = await PlaceOrderAsync(factory, "customer-without-cart");

        Assert.False(result.Succeeded);
        Assert.Equal("Seu carrinho está vazio.", result.Error);
    }

    [Fact]
    public async Task PlaceOrder_LastUnit_TwoCustomersAtTheSameTime_OnlyOneGetsIt()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var lastUnit = await NewProductAsync(factory, "Kit Teste Última Unidade", stock: 1);
        await AddToCartAsync(factory, "customer-race-a", lastUnit, 1);
        await AddToCartAsync(factory, "customer-race-b", lastUnit, 1);

        var results = await Task.WhenAll(
            PlaceOrderAsync(factory, "customer-race-a"),
            PlaceOrderAsync(factory, "customer-race-b"));

        Assert.Single(results, r => r.Succeeded);
        Assert.Equal(0, await StockOfAsync(factory, lastUnit));
    }
}
