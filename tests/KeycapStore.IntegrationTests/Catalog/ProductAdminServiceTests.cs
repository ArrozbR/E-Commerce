using KeycapStore.Application.Catalog;
using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Catalog;

public class ProductAdminServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<T> RunAsync<T>(WebApplicationFactory<Program> factory, Func<IProductAdminService, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<IProductAdminService>());
    }

    private static async Task RunAsync(WebApplicationFactory<Program> factory, Func<IProductAdminService, Task> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<IProductAdminService>());
    }

    private static async Task<Product> ProductAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking().SingleAsync(p => p.Id == productId);
    }

    private static Task<Guid> NewProductAsync(WebApplicationFactory<Program> factory, int stock) =>
        RunAsync(factory, s => s.CreateAsync($"Kit Admin {Guid.NewGuid():N}", 100m, stock));

    [Fact]
    public async Task Create_SavesAProductOnSale()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var productId = await RunAsync(factory, s => s.CreateAsync("Kit Novo do Admin", 199.90m, 7));

        var product = await ProductAsync(factory, productId);
        Assert.Equal("Kit Novo do Admin", product.Name);
        Assert.Equal(199.90m, product.Price);
        Assert.Equal(7, product.Stock);
        Assert.True(product.IsActive);
    }

    [Fact]
    public async Task Create_WithPriceZero_IsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RunAsync(factory, s => s.CreateAsync("Kit Sem Preço", 0m, 1)));
    }

    [Fact]
    public async Task Update_ChangesNameAndPrice_AndKeepsTheStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await NewProductAsync(factory, stock: 5);

        await RunAsync(factory, s => s.UpdateAsync(productId, "Kit Renomeado", 120m));

        var product = await ProductAsync(factory, productId);
        Assert.Equal("Kit Renomeado", product.Name);
        Assert.Equal(120m, product.Price);
        Assert.Equal(5, product.Stock);
    }

    [Fact]
    public async Task Update_UnknownProduct_IsNotFound()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await Assert.ThrowsAsync<ProductNotFoundException>(() => RunAsync(factory, s => s.UpdateAsync(Guid.NewGuid(), "Kit", 100m)));
    }

    [Fact]
    public async Task AdjustStock_AddsAndRemoves()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await NewProductAsync(factory, stock: 5);

        Assert.Equal(StockAdjustmentResult.Adjusted, await RunAsync(factory, s => s.AdjustStockAsync(productId, 10)));
        Assert.Equal(15, (await ProductAsync(factory, productId)).Stock);

        Assert.Equal(StockAdjustmentResult.Adjusted, await RunAsync(factory, s => s.AdjustStockAsync(productId, -3)));
        Assert.Equal(12, (await ProductAsync(factory, productId)).Stock);
    }

    [Fact]
    public async Task AdjustStock_DownToExactlyZero_IsAccepted()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await NewProductAsync(factory, stock: 5);

        Assert.Equal(StockAdjustmentResult.Adjusted, await RunAsync(factory, s => s.AdjustStockAsync(productId, -5)));
        Assert.Equal(0, (await ProductAsync(factory, productId)).Stock);
    }

    [Fact]
    public async Task AdjustStock_BelowZero_IsRefused_AndKeepsTheStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await NewProductAsync(factory, stock: 5);

        Assert.Equal(StockAdjustmentResult.WouldGoNegative, await RunAsync(factory, s => s.AdjustStockAsync(productId, -6)));
        Assert.Equal(5, (await ProductAsync(factory, productId)).Stock);
    }

    [Fact]
    public async Task AdjustStock_ByZero_IsRefused()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await NewProductAsync(factory, stock: 5);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => RunAsync(factory, s => s.AdjustStockAsync(productId, 0)));
    }

    [Fact]
    public async Task AdjustStock_UnknownProduct_IsNotFound()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        await Assert.ThrowsAsync<ProductNotFoundException>(() => RunAsync(factory, s => s.AdjustStockAsync(Guid.NewGuid(), 1)));
    }

    [Fact]
    public async Task AdjustStock_ManyAdditionsAtTheSameTime_AllCount()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await NewProductAsync(factory, stock: 0);

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => RunAsync(factory, s => s.AdjustStockAsync(productId, 1))));

        Assert.Equal(10, (await ProductAsync(factory, productId)).Stock);
    }

    [Fact]
    public async Task AdjustStock_ManyRemovalsAtTheSameTime_NeverGoBelowZero()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await NewProductAsync(factory, stock: 5);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => RunAsync(factory, s => s.AdjustStockAsync(productId, -1))));

        Assert.Equal(5, results.Count(r => r == StockAdjustmentResult.Adjusted));
        Assert.Equal(5, results.Count(r => r == StockAdjustmentResult.WouldGoNegative));
        Assert.Equal(0, (await ProductAsync(factory, productId)).Stock);
    }

    [Fact]
    public async Task Deactivate_ThenActivate_GoesBackOnSale()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var productId = await NewProductAsync(factory, stock: 5);

        await RunAsync(factory, s => s.DeactivateAsync(productId));
        Assert.False((await ProductAsync(factory, productId)).IsActive);

        await RunAsync(factory, s => s.ActivateAsync(productId));
        Assert.True((await ProductAsync(factory, productId)).IsActive);
    }
}
