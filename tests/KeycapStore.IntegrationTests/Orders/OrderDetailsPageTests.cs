using System.Net;

using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class OrderDetailsPageTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static async Task<(HttpClient Client, Guid OrderId)> OrderPageAsync(WebApplicationFactory<Program> factory, Action<Order> advance)
    {
        var email = $"pedido-{Guid.NewGuid():N}@example.com";
        var client = await PageClient.LoggedInAsync(factory, email);

        await using var scope = factory.Services.CreateAsyncScope();
        var customerId = (await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync(email))!.Id;
        var product = new Product($"Kit Pedido {Guid.NewGuid():N}", 100m, 3, TestPackage.Standard);
        var order = new Order(customerId, DateTimeOffset.UtcNow, [new OrderItem(product.Id, product.Name, product.Price, 2)], Curitiba, TestShipping.Pac);
        advance(order);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return (client, order.Id);
    }

    [Fact]
    public async Task ShippedOrder_ShowsTheTrail_UpToShipped_TheTrackingCode_AndTheWithdrawal()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (client, orderId) = await OrderPageAsync(factory, o =>
        {
            o.ConfirmPayment("pi_test_trail");
            o.StartPicking();
            o.MarkShipped("BR123456789BR");
        });

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{orderId}"));

        Assert.Contains($"<h1 class=\"mb-1\">Pedido {orderId.ToString()[..8]}</h1>", html);
        Assert.Contains("<span class=\"badge badge-success px-3 py-1 text-sm\">Enviado</span>", html);
        Assert.Matches("data-state=\"done\" aria-current=\"step\">[\\s\\S]*?<span>Enviado</span>", html);
        Assert.Contains("data-state=\"next\"", html);
        Assert.Contains("<code class=\"tracking-code\">BR123456789BR</code>", html);
        Assert.Contains("Desistir da compra", html);
        Assert.DoesNotContain("Cancelar pedido", html);
    }

    [Fact]
    public async Task ExpiredOrder_ExplainsWhatHappened_InsteadOfTheTrail()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (client, orderId) = await OrderPageAsync(factory, o => o.Expire());

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{orderId}"));

        Assert.Contains("O tempo para pagar acabou e os itens voltaram para a loja. Nada foi cobrado.", html);
        Assert.DoesNotContain("Andamento do pedido", html);
        Assert.DoesNotContain("tracking-code", html);
    }

    [Fact]
    public async Task OrderAwaitingPayment_HasThePaymentAsTheCurrentStep_AndItsActions()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (client, orderId) = await OrderPageAsync(factory, _ => { });

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Orders/Details/{orderId}"));

        Assert.Matches("data-state=\"current\" aria-current=\"step\">[\\s\\S]*?<span>Pagamento</span>", html);
        Assert.Contains("O que você pode fazer", html);
        Assert.Contains("Continuar o pagamento", html);
        Assert.Contains("<span class=\"badge badge-warning px-3 py-1 text-sm\">Aguardando pagamento</span>", html);
    }

    [Fact]
    public async Task ReturnPage_ShowsTheStatusAsABadge_AndAsksTheScriptToFollowIt()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (client, orderId) = await OrderPageAsync(factory, o => o.ConfirmPayment("pi_test_return"));

        var html = WebUtility.HtmlDecode(await client.GetStringAsync($"/orders/{orderId}/return"));
        var json = await client.GetStringAsync($"/orders/{orderId}/status");

        Assert.Contains("Obrigado pela compra!", html);
        Assert.Contains("class=\"thanks-key is-confirmed\"", html);
        Assert.Contains("id=\"order-status\" class=\"badge badge-success px-3 py-1 text-sm\">Pago<", html);
        Assert.Contains($"data-status-url=\"/orders/{orderId}/status\"", html);
        Assert.Contains("src=\"/js/order-return.js", html);
        Assert.Contains("\"tone\":\"success\"", json);
    }
}
