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

public class StripeWebhookConfirmPaymentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private const long OrderTotalInCents = 21500;

    private static async Task<(Guid OrderId, Guid ProductId)> PlaceOrderAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        var product = new Product($"Kit Webhook {customerId}", 100m, 5);
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

    private static async Task<OrderStatus> StatusOfAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
    }

    [Fact]
    public async Task Completed_AndPaid_ConfirmsTheOrder_WithoutTouchingTheStock()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, productId) = await PlaceOrderAsync(factory, "customer-paid");

        var response = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_test_paid", orderId, OrderTotalInCents));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.Paid, await StatusOfAsync(factory, orderId));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(3, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task Completed_ButUnpaid_LeavesTheOrderAwaitingConfirmation()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, _) = await PlaceOrderAsync(factory, "customer-unpaid");

        var response = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_test_unpaid", orderId, OrderTotalInCents, paymentStatus: "unpaid"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.AwaitingConfirmation, await StatusOfAsync(factory, orderId));
    }

    [Theory]
    [InlineData(OrderTotalInCents - 1, "brl")]
    [InlineData(OrderTotalInCents, "usd")]
    public async Task Completed_WithAnotherAmountOrCurrency_DoesNotConfirm(long amountTotal, string currency)
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (orderId, _) = await PlaceOrderAsync(factory, $"customer-divergent-{amountTotal}-{currency}");

        var response = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted($"evt_test_divergent_{amountTotal}_{currency}", orderId, amountTotal, currency));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(OrderStatus.AwaitingPayment, await StatusOfAsync(factory, orderId));
    }

    [Fact]
    public async Task Completed_ForAnUnknownOrder_Returns200()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();

        var response = await SendAsync(factory, StripeWebhookSigner.CheckoutCompleted("evt_test_unknown", Guid.NewGuid(), OrderTotalInCents));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
