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

public class StripeWebhookLatePaymentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private const string Succeeded = "checkout.session.async_payment_succeeded";
    private const string Failed = "checkout.session.async_payment_failed";

    private static async Task<(Guid OrderId, Guid[] ProductIds, long TotalInCents)> PlaceOrderAsync(
        WebApplicationFactory<Program> factory, string customerId, int productCount = 1)
    {
        var products = Enumerable.Range(0, productCount)
            .Select(i => new Product($"Kit Atrasado {customerId} {i}", 100m, 5))
            .ToArray();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
        foreach (var product in products)
        {
            await carts.AddItemAsync(customerId, product.Id, 2);
        }

        var placed = await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceOrderAsync(customerId, Curitiba);
        var totalInCents = (productCount * 20000L) + 1500;

        return (placed.OrderId!.Value, products.Select(p => p.Id).ToArray(), totalInCents);
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

    private static async Task SetStockAsync(WebApplicationFactory<Program> factory, Guid productId, int stock)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.Where(p => p.Id == productId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, stock));
    }

    private static async Task<int> StockAsync(WebApplicationFactory<Program> factory, Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync();
    }

    private static async Task<Order> OrderAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.AsNoTracking().SingleAsync(o => o.Id == orderId);
    }

    [Fact]
    public async Task PaidAfterExpired_WithStockLeft_ReservesAgainAndGoesToPaid()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productIds, total) = await PlaceOrderAsync(factory, "customer-late-stock");
        await SendAsync(factory, StripeWebhookSigner.CheckoutExpired("evt_late_expired_1", orderId));

        var response = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_late_paid_1", orderId, total));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(factory, orderId)).Status);
        Assert.Equal(3, await StockAsync(factory, productIds[0]));
    }

    [Fact]
    public async Task PaidAfterExpired_WithoutStock_GoesToPaidOutOfStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productIds, total) = await PlaceOrderAsync(factory, "customer-late-nostock");
        await SendAsync(factory, StripeWebhookSigner.CheckoutExpired("evt_late_expired_2", orderId));
        await SetStockAsync(factory, productIds[0], 1);

        var response = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_late_paid_2", orderId, total));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await OrderAsync(factory, orderId);
        Assert.Equal(OrderStatus.PaidOutOfStock, order.Status);
        Assert.Equal($"pi_test_{orderId:N}", order.StripePaymentIntentId);
        Assert.Equal(1, await StockAsync(factory, productIds[0]));
    }

    [Fact]
    public async Task PaidAfterExpired_OnlyOneProductMissing_DoesNotKeepHalfAReservation()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productIds, total) = await PlaceOrderAsync(factory, "customer-late-half", productCount: 2);
        await SendAsync(factory, StripeWebhookSigner.CheckoutExpired("evt_late_expired_3", orderId));
        var available = productIds.Min();
        var missing = productIds.Max();
        await SetStockAsync(factory, missing, 0);

        await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_late_paid_3", orderId, total));

        Assert.Equal(OrderStatus.PaidOutOfStock, (await OrderAsync(factory, orderId)).Status);
        Assert.Equal(5, await StockAsync(factory, available));
        Assert.Equal(0, await StockAsync(factory, missing));
    }

    [Fact]
    public async Task AsyncSucceededAfterAsyncFailed_WithStockLeft_GoesToPaid()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productIds, total) = await PlaceOrderAsync(factory, "customer-late-pix");
        await SendAsync(factory, StripeWebhookSigner.AsyncPayment("evt_late_failed_4", Failed, orderId, total, "unpaid"));

        var response = await SendAsync(factory, StripeWebhookSigner.AsyncPayment("evt_late_succeeded_4", Succeeded, orderId, total, "paid"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.Paid, (await OrderAsync(factory, orderId)).Status);
        Assert.Equal(3, await StockAsync(factory, productIds[0]));
    }

    [Fact]
    public async Task AnotherPaidEvent_ForPaidOutOfStock_IsIgnored()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productIds, total) = await PlaceOrderAsync(factory, "customer-late-repeat");
        await SendAsync(factory, StripeWebhookSigner.CheckoutExpired("evt_late_expired_5", orderId));
        await SetStockAsync(factory, productIds[0], 0);
        await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_late_paid_5a", orderId, total));
        await SetStockAsync(factory, productIds[0], 5);

        var response = await SendAsync(factory, StripeWebhookSigner.AsyncPayment("evt_late_paid_5b", Succeeded, orderId, total, "paid"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.PaidOutOfStock, (await OrderAsync(factory, orderId)).Status);
        Assert.Equal(5, await StockAsync(factory, productIds[0]));
    }
}
