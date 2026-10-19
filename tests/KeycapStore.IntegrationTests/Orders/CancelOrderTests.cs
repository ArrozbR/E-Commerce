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

public class CancelOrderTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<string> UserIdAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }

    private static async Task<(Guid OrderId, Guid ProductId)> PaidOrderAsync(
        WebApplicationFactory<Program> factory, string customerId, Action<Order>? advance = null)
    {
        var product = new Product($"Kit Cancelamento {Guid.NewGuid():N}", 100m, 3);
        var order = new Order(customerId, DateTimeOffset.UtcNow, [new OrderItem(product.Id, product.Name, product.Price, 2)], Curitiba);
        order.ConfirmPayment("pi_test_cancel");
        advance?.Invoke(order);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (order.Id, product.Id);
    }

    private static async Task<HttpResponseMessage> CancelAsync(HttpClient client, Guid orderId)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));

        return await client.PostAsync($"/orders/{orderId}/cancel",
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
    public async Task Owner_CancelsAPaidOrder_AndTheStockComesBack()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "cancela-pago@example.com");
        var (orderId, productId) = await PaidOrderAsync(factory, await UserIdAsync(factory, "cancela-pago@example.com"));

        var response = await CancelAsync(client, orderId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal((OrderStatus.Cancelled, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Owner_CancelsWhilePicking()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "cancela-separando@example.com");
        var (orderId, productId) = await PaidOrderAsync(factory, await UserIdAsync(factory, "cancela-separando@example.com"), o => o.StartPicking());

        await CancelAsync(client, orderId);

        Assert.Equal((OrderStatus.Cancelled, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task ShippedOrder_Gets409_AndNothingChanges()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "cancela-enviado@example.com");
        var (orderId, productId) = await PaidOrderAsync(factory, await UserIdAsync(factory, "cancela-enviado@example.com"),
            o => { o.StartPicking(); o.MarkShipped("BR123456789"); });

        var response = await CancelAsync(client, orderId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal((OrderStatus.Shipped, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task AnotherCustomer_Gets404_AndNothingChanges()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        await PageClient.LoggedInAsync(factory, "dono@example.com");
        var (orderId, productId) = await PaidOrderAsync(factory, await UserIdAsync(factory, "dono@example.com"));
        var stranger = await PageClient.LoggedInAsync(factory, "curioso-cancela@example.com");

        var response = await CancelAsync(stranger, orderId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task OrderPage_ShowsTheCancelButton_OnlyBeforeShipping()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "botao-cancelar@example.com");
        var customerId = await UserIdAsync(factory, "botao-cancelar@example.com");
        var (paid, _) = await PaidOrderAsync(factory, customerId);
        var (shipped, _) = await PaidOrderAsync(factory, customerId, o => { o.StartPicking(); o.MarkShipped("BR1"); });

        Assert.Contains("Cancelar pedido", WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{paid}")));
        Assert.DoesNotContain("Cancelar pedido", WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{shipped}")));
    }
}
