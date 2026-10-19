using System.Net;

using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class WithdrawOrderTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<string> UserIdAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }

    private static async Task<(Guid OrderId, Guid ProductId)> ShippedOrderAsync(
        WebApplicationFactory<Program> factory, string customerId, DateTimeOffset? deliveredAt = null)
    {
        var product = new Product($"Kit Arrependimento {Guid.NewGuid():N}", 100m, 3);
        var order = new Order(customerId, DateTimeOffset.UtcNow, [new OrderItem(product.Id, product.Name, product.Price, 2)], Curitiba);
        order.ConfirmPayment("pi_test_withdraw");
        order.StartPicking();
        order.MarkShipped("BR123456789");
        if (deliveredAt is { } when)
        {
            order.MarkDelivered(when);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (order.Id, product.Id);
    }

    private static async Task<HttpResponseMessage> WithdrawAsync(HttpClient client, Guid orderId)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));

        return await client.PostAsync($"/orders/{orderId}/withdraw",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));
    }

    private static async Task<(OrderStatus Status, int Stock)> StateAsync(WebApplicationFactory<Program> factory, Guid orderId, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var status = await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
        var stock = await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync();
        return (status, stock);
    }

    [Fact]
    public async Task Owner_WithdrawsADeliveredOrder_AndTheStockDoesNotChange()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "desiste-entregue@example.com");
        var (orderId, productId) = await ShippedOrderAsync(factory, await UserIdAsync(factory, "desiste-entregue@example.com"), DateTimeOffset.UtcNow.AddDays(-2));

        var response = await WithdrawAsync(client, orderId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal((OrderStatus.Withdrawn, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Owner_WithdrawsWhileShipped()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "desiste-a-caminho@example.com");
        var (orderId, productId) = await ShippedOrderAsync(factory, await UserIdAsync(factory, "desiste-a-caminho@example.com"));

        await WithdrawAsync(client, orderId);

        Assert.Equal((OrderStatus.Withdrawn, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task AfterSevenDays_Gets409_AndNothingChanges()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "desiste-tarde@example.com");
        var (orderId, productId) = await ShippedOrderAsync(factory, await UserIdAsync(factory, "desiste-tarde@example.com"), DateTimeOffset.UtcNow.AddDays(-8));

        var response = await WithdrawAsync(client, orderId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal((OrderStatus.Delivered, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task AnotherCustomer_Gets404_AndNothingChanges()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await PageClient.LoggedInAsync(factory, "dono-desiste@example.com");
        var (orderId, productId) = await ShippedOrderAsync(factory, await UserIdAsync(factory, "dono-desiste@example.com"), DateTimeOffset.UtcNow);
        var stranger = await PageClient.LoggedInAsync(factory, "curioso-desiste@example.com");

        var response = await WithdrawAsync(stranger, orderId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal((OrderStatus.Delivered, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task OrderPage_ShowsTheWithdrawButton_OnlyWithinTheSevenDays()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "botao-desistir@example.com");
        var customerId = await UserIdAsync(factory, "botao-desistir@example.com");
        var (recent, _) = await ShippedOrderAsync(factory, customerId, DateTimeOffset.UtcNow.AddDays(-1));
        var (old, _) = await ShippedOrderAsync(factory, customerId, DateTimeOffset.UtcNow.AddDays(-8));

        Assert.Contains("Desistir da compra", WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{recent}")));
        Assert.DoesNotContain("Desistir da compra", WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{old}")));
    }
}
