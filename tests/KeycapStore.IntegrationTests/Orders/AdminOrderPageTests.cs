using System.Net;

using KeycapStore.Application.Identity;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class AdminOrderPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly Guid KitAurora = Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01");

    private static async Task<Guid> CreateOrderAsync(WebApplicationFactory<Program> factory, string recipient, bool paid)
    {
        var address = new ShippingAddress(recipient, "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");
        var order = new Order("customer-admin-page", DateTimeOffset.UtcNow, [new OrderItem(KitAurora, "Kit Aurora (base)", 349.90m, 1)], address);
        if (paid)
        {
            order.ConfirmPayment();
        }

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

        var promoted = await factory.Services.CreateAdminAsync(email);
        Assert.True(promoted.Succeeded);

        return await PageClient.LoggedInAsync(factory, email);
    }

    [Fact]
    public async Task Visitor_IsSentToLogin()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await PageClient.Anonymous(factory).GetAsync("/admin/orders");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Customer_GetsForbidden()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var client = await PageClient.LoggedInAsync(factory, "cliente-comum@example.com");

        var response = await client.GetAsync("/admin/orders");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_SeesPaidOrdersWithTheAddress_ButNotUnpaidOnes()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var paid = await CreateOrderAsync(factory, "Destinatário Pago", paid: true);
        var unpaid = await CreateOrderAsync(factory, "Destinatário Sem Pagamento", paid: false);
        var client = await AdminClientAsync(factory, "admin-envio@example.com");

        var response = await client.GetAsync("/admin/orders");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(paid.ToString(), html);
        Assert.Contains("Destinatário Pago", html);
        Assert.Contains("Rua XV de Novembro, 200", html);
        Assert.DoesNotContain(unpaid.ToString(), html);
    }
}
