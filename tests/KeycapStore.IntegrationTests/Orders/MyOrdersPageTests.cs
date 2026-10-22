using System.Net;

using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class MyOrdersPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<string> UserIdAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email))!.Id;
    }

    private static async Task<Guid> SaveOrderAsync(WebApplicationFactory<Program> factory, string customerId, DateTimeOffset createdAt)
    {
        var order = new Order(customerId, createdAt, [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Curitiba);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order.Id;
    }

    [Fact]
    public async Task Visitor_IsSentToLogin()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync("/orders");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Customer_SeesOnlyTheirOwnOrders_NewestFirst()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "lista-dono@example.com");
        var customerId = await UserIdAsync(factory, "lista-dono@example.com");
        var older = await SaveOrderAsync(factory, customerId, DateTimeOffset.UtcNow.AddDays(-2));
        var newer = await SaveOrderAsync(factory, customerId, DateTimeOffset.UtcNow.AddDays(-1));
        await PageClient.LoggedInAsync(factory, "lista-outro@example.com");
        var someoneElses = await SaveOrderAsync(factory, await UserIdAsync(factory, "lista-outro@example.com"), DateTimeOffset.UtcNow);

        var html = await client.GetStringAsync("/orders");

        Assert.Contains(older.ToString(), html);
        Assert.Contains(newer.ToString(), html);
        Assert.True(html.IndexOf(newer.ToString(), StringComparison.Ordinal) < html.IndexOf(older.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(someoneElses.ToString(), html);
    }

    [Fact]
    public async Task CustomerWithoutOrders_SeesTheEmptyMessage()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "lista-vazia@example.com");

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/orders"));

        Assert.Contains("Você ainda não fez nenhum pedido.", html);
    }
}
