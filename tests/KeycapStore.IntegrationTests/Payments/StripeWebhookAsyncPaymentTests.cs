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

public class StripeWebhookAsyncPaymentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private const long OrderTotalInCents = 21500;

    private const string Succeeded = "checkout.session.async_payment_succeeded";
    private const string Failed = "checkout.session.async_payment_failed";

    private static async Task<(Guid OrderId, Guid ProductId)> PlaceOrderAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        var product = new Product($"Kit Pix {customerId}", 100m, 5);
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
    public async Task Succeeded_AfterAwaitingConfirmation_ConfirmsTheOrder()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "pix-succeeded");
        await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_pix_completed_unpaid", orderId, OrderTotalInCents, paymentStatus: "unpaid"));

        var response = await SendAsync(factory, StripeWebhookSigner.AsyncPayment("evt_pix_succeeded", Succeeded, orderId, OrderTotalInCents, "paid"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Succeeded_BeforeCompleted_StaysPaid()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "pix-out-of-order");

        await SendAsync(factory, StripeWebhookSigner.AsyncPayment("evt_pix_early_succeeded", Succeeded, orderId, OrderTotalInCents, "paid"));
        var late = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_pix_late_completed", orderId, OrderTotalInCents, paymentStatus: "unpaid"));

        Assert.Equal(HttpStatusCode.OK, late.StatusCode);
        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Failed_AfterAwaitingConfirmation_FailsTheOrder_AndReleasesTheStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "pix-failed");
        await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_pix_failed_unpaid", orderId, OrderTotalInCents, paymentStatus: "unpaid"));

        var response = await SendAsync(factory, StripeWebhookSigner.AsyncPayment("evt_pix_failed", Failed, orderId, OrderTotalInCents, "unpaid"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((OrderStatus.Failed, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Failed_TenDifferentEventsAtTheSameTime_ReleaseTheStockOnce()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "pix-failed-parallel");

        await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(i => SendAsync(factory, StripeWebhookSigner.AsyncPayment($"evt_pix_failed_{i}", Failed, orderId, OrderTotalInCents, "unpaid"))));

        Assert.Equal((OrderStatus.Failed, 5), await StateAsync(factory, orderId, productId));
    }

    [Fact]
    public async Task Failed_AfterThePayment_DoesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "pix-paid-then-failed");
        await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_pix_paid_first", orderId, OrderTotalInCents));

        var response = await SendAsync(factory, StripeWebhookSigner.AsyncPayment("evt_pix_failed_late", Failed, orderId, OrderTotalInCents, "unpaid"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((OrderStatus.Paid, 3), await StateAsync(factory, orderId, productId));
    }
}
