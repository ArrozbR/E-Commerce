using System.Net;

using KeycapStore.Application.Identity;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class AdminReturnTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<(Guid OrderId, Guid ProductId)> WithdrawnOrderAsync(WebApplicationFactory<Program> factory, bool refunded)
    {
        var product = new Product($"Kit Devolução {Guid.NewGuid():N}", 100m, 3);
        var order = new Order("customer-return", DateTimeOffset.UtcNow, [new OrderItem(product.Id, product.Name, product.Price, 2)], Curitiba);
        order.ConfirmPayment("pi_test_return");
        order.StartPicking();
        order.MarkShipped("BR123456789");
        order.MarkDelivered(DateTimeOffset.UtcNow);
        order.Withdraw(DateTimeOffset.UtcNow);
        if (refunded)
        {
            order.MarkRefunded();
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (order.Id, product.Id);
    }

    private static async Task<HttpClient> AdminClientAsync(WebApplicationFactory<Program> factory, string email)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync(email, PageClient.Password);
        }

        Assert.True((await factory.Services.CreateAdminAsync(email)).Succeeded);

        return await PageClient.LoggedInAsync(factory, email);
    }

    private static async Task<HttpResponseMessage> ConfirmReturnAsync(HttpClient client, Guid orderId, bool restock)
    {
        var form = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Catalog")),
        };
        if (restock)
        {
            form["restock"] = "true";
        }

        return await client.PostAsync($"/admin/orders/{orderId}/confirm-return", new FormUrlEncodedContent(form));
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
    public async Task ReturnReceived_WithRestock_GoesToReturned_AndTheStockComesBack()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminClientAsync(factory, "admin-devolve-estoque@example.com");
        var (orderId, productId) = await WithdrawnOrderAsync(factory, refunded: true);

        var response = await ConfirmReturnAsync(admin, orderId, restock: true);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal((OrderStatus.Returned, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task ReturnReceived_WithoutRestock_KeepsTheStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminClientAsync(factory, "admin-devolve-danificado@example.com");
        var (orderId, productId) = await WithdrawnOrderAsync(factory, refunded: true);

        await ConfirmReturnAsync(admin, orderId, restock: false);

        Assert.Equal((OrderStatus.Returned, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task ReturnReceived_BeforeTheRefund_Gets409_AndNothingChanges()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminClientAsync(factory, "admin-devolve-cedo@example.com");
        var (orderId, productId) = await WithdrawnOrderAsync(factory, refunded: false);

        var response = await ConfirmReturnAsync(admin, orderId, restock: true);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal((OrderStatus.Withdrawn, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Customer_CannotConfirmAReturn()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var customer = await PageClient.LoggedInAsync(factory, "cliente-devolve@example.com");
        var (orderId, productId) = await WithdrawnOrderAsync(factory, refunded: true);

        var response = await ConfirmReturnAsync(customer, orderId, restock: true);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal((OrderStatus.AwaitingReturn, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task AdminPage_ListsTheOrderAwaitingReturn_WithTheButton()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminClientAsync(factory, "admin-fila-devolucao@example.com");
        var (orderId, _) = await WithdrawnOrderAsync(factory, refunded: true);

        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/orders"));

        Assert.Contains(orderId.ToString(), html);
        Assert.Contains("Devolução recebida", html);
    }
}
