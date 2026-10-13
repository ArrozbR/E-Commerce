using System.Net;
using System.Text;

using KeycapStore.Application.Carts;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Payments;

public class StripeWebhookExpiredTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    // 2 × R$ 100,00 + frete de R$ 15,00 (PR) = R$ 215,00 = 21500 centavos.
    private const long OrderTotalInCents = 21500;

    // Cria um produto com 5 unidades, compra 2 e devolve o pedido (o estoque fica em 3).
    private static async Task<(Guid OrderId, Guid ProductId)> PlaceOrderAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        var product = new Product($"Kit Expira {customerId}", 100m, 5);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync(customerId, product.Id, 2);
        var placed = await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceOrderAsync(customerId, Curitiba);

        return (placed.OrderId!.Value, product.Id);
    }

    private static async Task<HttpResponseMessage> SendAsync(WebApplicationFactory<Program> factory, string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", StripeWebhookSigner.Sign(payload));

        return await factory.CreateClient().SendAsync(request);
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
    public async Task Expired_ExpiresTheOrder_AndReleasesTheStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "customer-expired");

        var response = await SendAsync(factory, StripeWebhookSigner.CheckoutExpired("evt_test_expired", orderId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((OrderStatus.Expired, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Expired_SameEventTwice_ReleasesTheStockOnce()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "customer-expired-twice");
        var payload = StripeWebhookSigner.CheckoutExpired("evt_test_expired_twice", orderId);

        await SendAsync(factory, payload);
        var second = await SendAsync(factory, payload);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((OrderStatus.Expired, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Expired_TenDifferentEventsAtTheSameTime_ReleaseTheStockOnce()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "customer-expired-parallel");

        // Dez eventos DIFERENTES (o caderno não barra) chegando juntos: quem barra é o token de concorrência.
        // Sem o token, o estoque voltava 10 vezes (ia de 3 para 23).
        await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(i => SendAsync(factory, StripeWebhookSigner.CheckoutExpired($"evt_test_expired_{i}", orderId))));

        Assert.Equal((OrderStatus.Expired, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Expired_AfterThePayment_DoesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "customer-paid-then-expired");
        await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_test_paid_first", orderId, OrderTotalInCents));

        var response = await SendAsync(factory, StripeWebhookSigner.CheckoutExpired("evt_test_expired_late", orderId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Completed_TwoDifferentEventsForTheSameOrder_BothGet200()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "customer-paid-twice");

        var first = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_test_paid_a", orderId, OrderTotalInCents));
        var second = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_test_paid_b", orderId, OrderTotalInCents));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }
}
