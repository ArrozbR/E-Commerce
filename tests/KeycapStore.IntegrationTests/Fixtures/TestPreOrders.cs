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

    public static async Task EndCampaignAsync(WebApplicationFactory<Program> factory, Guid productId, TimeSpan? ago = null)
    {
        var endedAt = DateTimeOffset.UtcNow - (ago ?? TimeSpan.FromMinutes(1));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.PreOrderEndsAt, endedAt));
    }

    public static async Task<Guid> SaveOrderAsync(WebApplicationFactory<Program> factory, Guid productId, int quantity, Action<KeycapStore.Domain.Orders.Order>? advance = null, string? customerId = null)
    {
        var order = new KeycapStore.Domain.Orders.Order(
            customerId ?? $"customer-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow,
            [new KeycapStore.Domain.Orders.OrderItem(productId, "Kit Pré-venda", 100m, quantity)],
            new KeycapStore.Domain.Orders.ShippingAddress("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310"),
            TestShipping.Pac,
            KeycapStore.Domain.Orders.Order.DefaultLanguage,
            DateTimeOffset.UtcNow.AddDays(40));
        advance?.Invoke(order);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    public static async Task CloseAsync(WebApplicationFactory<Program> factory, Guid productId, bool confirmed)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.SingleAsync(p => p.Id == productId);
        if (confirmed)
        {
            product.ConfirmPreOrder();
        }
        else
        {
            product.CancelPreOrder();
        }

        await db.SaveChangesAsync();
    }
}
