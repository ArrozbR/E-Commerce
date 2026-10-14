using System.Net;

using KeycapStore.Application.Identity;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class AdminShippingActionsTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<Guid> CreatePaidOrderAsync(WebApplicationFactory<Program> factory)
    {
        var order = new Order("customer-shipping", DateTimeOffset.UtcNow, [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], Curitiba);
        order.ConfirmPayment();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order.Id;
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

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, Dictionary<string, string>? fields = null)
    {
        var form = new Dictionary<string, string>(fields ?? [])
        {
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/admin/orders")),
        };

        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    private static async Task<Order> OrderAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
    }

    [Fact]
    public async Task Admin_TakesAPaidOrderAllTheWayToDelivered()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var orderId = await CreatePaidOrderAsync(factory);
        var admin = await AdminClientAsync(factory, "admin-fluxo@example.com");

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(admin, $"/admin/orders/{orderId}/start-picking")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(admin, $"/admin/orders/{orderId}/ship", new() { ["trackingCode"] = "BR123456789" })).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(admin, $"/admin/orders/{orderId}/deliver")).StatusCode);

        var order = await OrderAsync(factory, orderId);
        Assert.Equal(OrderStatus.Delivered, order.Status);
        Assert.Equal("BR123456789", order.TrackingCode);
    }

    [Fact]
    public async Task StartPickingTwice_TheSecondGets409()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var orderId = await CreatePaidOrderAsync(factory);
        var admin = await AdminClientAsync(factory, "admin-duplo@example.com");

        await PostAsync(admin, $"/admin/orders/{orderId}/start-picking");
        var second = await PostAsync(admin, $"/admin/orders/{orderId}/start-picking");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(OrderStatus.Picking, (await OrderAsync(factory, orderId)).Status);
    }

    [Fact]
    public async Task Ship_WithoutTrackingCode_Gets400_AndStaysPicking()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var orderId = await CreatePaidOrderAsync(factory);
        var admin = await AdminClientAsync(factory, "admin-sem-rastreio@example.com");
        await PostAsync(admin, $"/admin/orders/{orderId}/start-picking");

        var response = await PostAsync(admin, $"/admin/orders/{orderId}/ship", new() { ["trackingCode"] = " " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(OrderStatus.Picking, (await OrderAsync(factory, orderId)).Status);
    }

    [Fact]
    public async Task UnknownOrder_Gets404()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var admin = await AdminClientAsync(factory, "admin-inexistente@example.com");

        var response = await PostAsync(admin, $"/admin/orders/{Guid.NewGuid()}/start-picking");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_CannotStartPicking()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var orderId = await CreatePaidOrderAsync(factory);
        var customer = await PageClient.LoggedInAsync(factory, "cliente-curioso@example.com");
        var token = PageClient.TokenFrom(await customer.GetStringAsync("/Catalog"));

        var response = await customer.PostAsync($"/admin/orders/{orderId}/start-picking",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(factory, orderId)).Status);
    }

    [Fact]
    public async Task FiveClicksAtTheSameTime_OnlyOneWins()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var orderId = await CreatePaidOrderAsync(factory);
        var admin = await AdminClientAsync(factory, "admin-pressa@example.com");
        var token = PageClient.TokenFrom(await admin.GetStringAsync("/admin/orders"));

        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            admin.PostAsync($"/admin/orders/{orderId}/start-picking",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token }))));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Redirect);
        Assert.Equal(OrderStatus.Picking, (await OrderAsync(factory, orderId)).Status);
    }
}
