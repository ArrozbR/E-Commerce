using System.Net;

using KeycapStore.Application.Identity;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Identity;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class PreOrderShippingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task StartPickingAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IShippingService>().StartPickingAsync(orderId);
    }

    private static async Task<OrderStatus> StatusAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
    }

    [Fact]
    public async Task PreOrder_CannotBePicked_BeforeTheBatchIsConfirmed()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Separação Cedo");
        var order = await TestPreOrders.SaveOrderAsync(factory, preOrder, 1, o => o.ConfirmPayment());

        await Assert.ThrowsAsync<InvalidOrderTransitionException>(() => StartPickingAsync(factory, order));

        Assert.Equal(OrderStatus.Paid, await StatusAsync(factory, order));
    }

    [Fact]
    public async Task PreOrder_IsPicked_AfterTheBatchIsConfirmed()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Separação Confirmada", minimum: 1);
        var order = await TestPreOrders.SaveOrderAsync(factory, preOrder, 1, o => o.ConfirmPayment());
        await TestPreOrders.CloseAsync(factory, preOrder, confirmed: true);

        await StartPickingAsync(factory, order);

        Assert.Equal(OrderStatus.Picking, await StatusAsync(factory, order));
    }

    [Fact]
    public async Task NormalOrder_IsPickedAsBefore()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var order = new Order("customer-normal-picking", DateTimeOffset.UtcNow,
            [new OrderItem(Guid.Parse("6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01"), "Kit Aurora (base)", 349.90m, 1)],
            new ShippingAddress("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310"),
            TestShipping.Pac);
        order.ConfirmPayment();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Orders.Add(order);
            await db.SaveChangesAsync();
        }

        await StartPickingAsync(factory, order.Id);

        Assert.Equal(OrderStatus.Picking, await StatusAsync(factory, order.Id));
    }

    [Fact]
    public async Task AdminPage_ShowsTheOrderWaitingForTheBatch_WithoutThePickingButton()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var preOrder = await TestPreOrders.SaveOpenAsync(factory, "Kit Fila do Admin");
        var order = await TestPreOrders.SaveOrderAsync(factory, preOrder, 1, o => o.ConfirmPayment());
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAccountService>().RegisterAsync("admin-fila-pre-venda@example.com", PageClient.Password);
        }

        Assert.True((await factory.Services.CreateAdminAsync("admin-fila-pre-venda@example.com")).Succeeded);
        var admin = await PageClient.LoggedInAsync(factory, "admin-fila-pre-venda@example.com");

        var html = WebUtility.HtmlDecode(await admin.GetStringAsync("/admin/orders"));

        Assert.Contains("Aguardando o fechamento do lote", html);
        Assert.DoesNotContain($"/admin/orders/{order}/start-picking", html);
    }

    [Theory]
    [InlineData(true, "Lote confirmado: o pedido será enviado perto da data prevista.")]
    [InlineData(false, "Lote cancelado: o mínimo não foi atingido e o valor é devolvido automaticamente.")]
    public async Task OrderPage_ShowsTheBatchOutcome(bool confirmed, string message)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var email = $"dono-lote-{Guid.NewGuid():N}@example.com";
        var client = await PageClient.LoggedInAsync(factory, email);
        string customerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            customerId = (await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email))!.Id;
        }

        var preOrder = await TestPreOrders.SaveOpenAsync(factory, $"Kit Resultado {Guid.NewGuid():N}");
        var order = await TestPreOrders.SaveOrderAsync(factory, preOrder, 1, o => o.ConfirmPayment(), customerId);
        await TestPreOrders.CloseAsync(factory, preOrder, confirmed);

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{order}"));

        Assert.Contains("Pré-venda · envio previsto em", html);
        Assert.Contains(message, html);
    }
}
