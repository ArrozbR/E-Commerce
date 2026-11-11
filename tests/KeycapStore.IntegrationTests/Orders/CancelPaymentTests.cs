using System.Net;

using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace KeycapStore.IntegrationTests.Orders;

public class CancelPaymentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string KitAurora = "6f1c2a7e-3b4d-4c8a-9e21-0a1b2c3d4e01";

    private static async Task<Guid> PlaceOrderAsync(HttpClient client)
    {
        await client.PostAsync("/Cart/Add", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["productId"] = KitAurora,
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Catalog")),
        }));

        var checkout = await client.PostAsync("/Checkout", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["RecipientName"] = "Bruno Lima",
            ["PostalCode"] = "80020-310",
            ["Street"] = "Rua XV de Novembro",
            ["Number"] = "200",
            ["District"] = "Centro",
            ["City"] = "Curitiba",
            ["State"] = "PR",
            ["ShippingServiceId"] = TestShipping.PacServiceId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["__RequestVerificationToken"] = PageClient.TokenFrom(await client.GetStringAsync("/Checkout")),
        }));
        Assert.Equal(HttpStatusCode.Redirect, checkout.StatusCode);

        return Guid.Parse(checkout.Headers.Location!.OriginalString.Split('/').Last());
    }

    private static async Task<HttpResponseMessage> CancelPaymentAsync(HttpClient client, Guid orderId)
    {
        var token = PageClient.TokenFrom(await client.GetStringAsync("/Catalog"));

        return await client.PostAsync($"/orders/{orderId}/cancel-payment", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));
    }

    private static async Task<OrderStatus> StatusAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.Where(o => o.Id == orderId).Select(o => o.Status).SingleAsync();
    }

    [Fact]
    public async Task CancelPayment_AsksTheStripeToExpireTheSession_AndLeavesTheOrderToTheWebhook()
    {
        var payments = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(payments);
        var client = await PageClient.LoggedInAsync(factory, "desiste-de-pagar@example.com");
        var orderId = await PlaceOrderAsync(client);

        var response = await CancelPaymentAsync(client, orderId);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"cs_test_fake_{orderId:N}", Assert.Single(payments.ExpiredSessions));
        Assert.Equal(OrderStatus.AwaitingPayment, await StatusAsync(factory, orderId));
        Assert.Contains("Pagamento cancelado.", WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location)));
    }

    [Fact]
    public async Task CancelPayment_OfSomeoneElsesOrder_Is404_AndTheStripeIsNotCalled()
    {
        var payments = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(payments);
        var owner = await PageClient.LoggedInAsync(factory, "dono-do-pedido@example.com");
        var orderId = await PlaceOrderAsync(owner);
        var stranger = await PageClient.LoggedInAsync(factory, "curioso@example.com");

        var response = await CancelPaymentAsync(stranger, orderId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(payments.ExpiredSessions);
    }

    [Fact]
    public async Task CancelPayment_OfAPaidOrder_ChangesNothing_AndTheStripeIsNotCalled()
    {
        var payments = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(payments);
        var client = await PageClient.LoggedInAsync(factory, "ja-pagou@example.com");
        var orderId = await PlaceOrderAsync(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var order = await db.Orders.SingleAsync(o => o.Id == orderId);
            order.ConfirmPayment();
            await db.SaveChangesAsync();
        }

        var response = await CancelPaymentAsync(client, orderId);

        Assert.Contains("não está mais aguardando pagamento", WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location)));
        Assert.Empty(payments.ExpiredSessions);
        Assert.Equal(OrderStatus.Paid, await StatusAsync(factory, orderId));
    }

    [Fact]
    public async Task CancelPayment_WhenTheStripeRefuses_ShowsTheMessage()
    {
        var payments = new FakePaymentGateway { FailExpirations = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(payments);
        var client = await PageClient.LoggedInAsync(factory, "stripe-recusa@example.com");
        var orderId = await PlaceOrderAsync(client);

        var response = await CancelPaymentAsync(client, orderId);

        Assert.Contains("Não foi possível cancelar o pagamento agora.", WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location)));
        Assert.Equal(OrderStatus.AwaitingPayment, await StatusAsync(factory, orderId));
    }

    private static async Task<long> TotalInCentsAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var order = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.SingleAsync(o => o.Id == orderId);
        return (long)decimal.Round(order.Total * 100m, 0, MidpointRounding.AwayFromZero);
    }

    [Fact]
    public async Task CancelPayment_WhenTheSessionHadAlreadyExpiredAtTheStripe_ExpiresTheOrderNow_AndSaysSo()
    {
        var payments = new FakePaymentGateway { FailExpirations = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(payments);
        var client = await PageClient.LoggedInAsync(factory, "sessao-ja-expirada@example.com");
        var orderId = await PlaceOrderAsync(client);
        payments.Sessions[$"cs_test_fake_{orderId:N}"] = new("expired", "unpaid", null, null, null, null);

        var response = await CancelPaymentAsync(client, orderId);

        Assert.Equal(OrderStatus.Expired, await StatusAsync(factory, orderId));
        Assert.Contains("O prazo de pagamento deste pedido já tinha acabado.", WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location)));
    }

    [Fact]
    public async Task CancelPayment_WhenTheSessionWasAlreadyPaid_ConfirmsTheOrder_AndSaysSo()
    {
        var payments = new FakePaymentGateway { FailExpirations = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(payments);
        var client = await PageClient.LoggedInAsync(factory, "sessao-ja-paga@example.com");
        var orderId = await PlaceOrderAsync(client);
        payments.Sessions[$"cs_test_fake_{orderId:N}"] = new("complete", "paid", await TotalInCentsAsync(factory, orderId), "brl", $"pi_test_{orderId:N}", null);

        var response = await CancelPaymentAsync(client, orderId);

        Assert.Equal(OrderStatus.Paid, await StatusAsync(factory, orderId));
        Assert.Contains("Este pedido já foi pago", WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location)));
    }

    [Fact]
    public async Task CancelPayment_WhenTheStripeIsDownForTheLookupToo_KeepsTheTryAgainMessage()
    {
        var payments = new FakePaymentGateway { FailExpirations = true, FailSessionLookups = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(payments);
        var client = await PageClient.LoggedInAsync(factory, "stripe-fora-de-vez@example.com");
        var orderId = await PlaceOrderAsync(client);

        var response = await CancelPaymentAsync(client, orderId);

        Assert.Equal(OrderStatus.AwaitingPayment, await StatusAsync(factory, orderId));
        Assert.Contains("Não foi possível cancelar o pagamento agora.", WebUtility.HtmlDecode(await client.GetStringAsync(response.Headers.Location)));
    }
}
