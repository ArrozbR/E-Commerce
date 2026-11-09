using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Notifications;
using KeycapStore.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Fixtures;

public static class TestEmails
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Recife =
        new("Carla Souza", "Rua da Aurora", "50", null, "Boa Vista", "Recife", "PE", "50050000");

    public static async Task<(string CustomerId, string Email)> CreateCustomerAsync(
        WebApplicationFactory<Program> factory, string domain = "example.com")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = $"cliente-{Guid.NewGuid():N}@{domain}";
        var user = new IdentityUser { UserName = email, Email = email };
        var result = await users.CreateAsync(user);
        Assert.True(result.Succeeded);

        return (user.Id, email);
    }

    public static async Task<Guid> SaveOrderAsync(
        WebApplicationFactory<Program> factory,
        string customerId,
        Action<Order>? change = null,
        string language = Order.DefaultLanguage,
        DateTimeOffset? preOrderShipsAt = null)
    {
        var order = new Order(customerId, DateTimeOffset.UtcNow, [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Recife, TestShipping.Pac, language, preOrderShipsAt);
        change?.Invoke(order);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order.Id;
    }

    public static async Task<Guid> QueueAsync(WebApplicationFactory<Program> factory, OutboxEmail email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OutboxEmails.Add(email);
        await db.SaveChangesAsync();

        return email.Id;
    }

    public static Task<Guid> QueueAsync(WebApplicationFactory<Program> factory, EmailKind kind, Guid orderId) =>
        QueueAsync(factory, new OutboxEmail(kind, orderId, DateTimeOffset.UtcNow.AddSeconds(-1)));

    public static async Task DispatchAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IEmailOutboxDispatcher>().SendPendingAsync();
    }

    public static async Task<OutboxEmail?> FindAsync(WebApplicationFactory<Program> factory, Guid emailId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxEmails.AsNoTracking().SingleOrDefaultAsync(e => e.Id == emailId);
    }
}
