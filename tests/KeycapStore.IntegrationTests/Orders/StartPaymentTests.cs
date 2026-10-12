using KeycapStore.Application.Carts;
using KeycapStore.Application.Orders;
using KeycapStore.Domain.Catalog;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class StartPaymentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly ShippingAddress Curitiba =
        new("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310");

    private static readonly Uri Success = new("https://loja.test/Orders/Details");
    private static readonly Uri Cancel = new("https://loja.test/Cart");

    private static async Task<(Guid OrderId, Guid ProductId)> PlaceOrderAsync(WebApplicationFactory<Program> factory, string customerId)
    {
        var product = new Product($"Kit Teste {customerId}", 100m, 5);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(product);
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<ICartService>().AddItemAsync(customerId, product.Id, 2);
        var placed = await scope.ServiceProvider.GetRequiredService<ICheckoutService>().PlaceOrderAsync(customerId, Curitiba);

        return (placed.OrderId!.Value, product.Id);
    }

    private static async Task<StartPaymentResult> StartPaymentAsync(WebApplicationFactory<Program> factory, Guid orderId, string customerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICheckoutService>()
            .StartPaymentAsync(orderId, customerId, Success, Cancel);
    }

    [Fact]
    public async Task StartPayment_AttachesTheSession_AndReturnsThePaymentUrl()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId) = await PlaceOrderAsync(factory, "customer-pay");

        var result = await StartPaymentAsync(factory, orderId, "customer-pay");

        Assert.True(result.Succeeded);
        Assert.Equal($"https://checkout.stripe.test/{orderId:N}", result.PaymentUrl!.ToString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        Assert.Equal($"cs_test_fake_{orderId:N}", order.StripeSessionId);
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        Assert.Equal(3, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task StartPayment_WhenStripeFails_MarksTheOrderFailed_AndReleasesTheStock()
    {
        var stripe = new FakePaymentGateway { Fail = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, productId) = await PlaceOrderAsync(factory, "customer-stripe-down");

        var result = await StartPaymentAsync(factory, orderId, "customer-stripe-down");

        Assert.False(result.Succeeded);
        Assert.Equal("Não foi possível iniciar o pagamento. Tente de novo em alguns minutos.", result.Error);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        Assert.Equal(OrderStatus.Failed, order.Status);
        Assert.Null(order.StripeSessionId);
        Assert.Equal(5, await db.Products.Where(p => p.Id == productId).Select(p => p.Stock).SingleAsync());
    }

    [Fact]
    public async Task StartPayment_ForAnotherCustomersOrder_Fails_WithoutCallingStripe()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (orderId, _) = await PlaceOrderAsync(factory, "customer-owner");

        var result = await StartPaymentAsync(factory, orderId, "customer-intruder");

        Assert.False(result.Succeeded);
        Assert.Equal("Pedido não encontrado.", result.Error);
        Assert.Empty(stripe.RequestedOrders);
    }
}
