using KeycapStore.Domain.Catalog;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Fixtures;

public static class TestPreOrders
{
    public static async Task<Guid> SaveOpenAsync(WebApplicationFactory<Program> factory, string name, int slots = 50, int minimum = 20)
    {
        var now = DateTimeOffset.UtcNow;
        var product = Product.ForPreOrder(name, 100m, slots, TestPackage.Standard, new PreOrder(now.AddDays(10), now.AddDays(40), minimum), now);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    public static async Task EndCampaignAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PreOrderEndsAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
    }

    public static async Task<Guid> SaveOrderAsync(WebApplicationFactory<Program> factory, Guid productId, int quantity, Action<KeycapStore.Domain.Orders.Order>? advance = null)
    {
        var order = new KeycapStore.Domain.Orders.Order(
            $"customer-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow,
            [new KeycapStore.Domain.Orders.OrderItem(productId, "Kit Pré-venda", 100m, quantity)],
            new KeycapStore.Domain.Orders.ShippingAddress("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310"),
            TestShipping.Pac);
        advance?.Invoke(order);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
