using System.Net;
using System.Text;

using KeycapStore.Application.Notifications;
using KeycapStore.Application.Orders;
using KeycapStore.Application.Payments;
using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Persistence;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Notifications;

public class OrderEmailTriggerTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const long OrderTotalInCents = 36490;

    private static async Task<HttpResponseMessage> SendWebhookAsync(WebApplicationFactory<Program> factory, string payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/stripe")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Stripe-Signature", StripeWebhookSigner.Sign(payload));

        return await factory.CreateClient().SendAsync(request);
    }

    private static async Task<List<EmailKind>> QueuedForAsync(WebApplicationFactory<Program> factory, Guid orderId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.OutboxEmails.Where(e => e.OrderId == orderId).Select(e => e.Kind).ToListAsync();
    }

    private static async Task<(string CustomerId, Guid OrderId)> OrderAsync(WebApplicationFactory<Program> factory, Action<Order>? change = null)
    {
        var (customerId, _) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId, change);
        return (customerId, orderId);
    }

    [Fact]
    public async Task Webhook_Paid_QueuesThePaidEmail_OnlyOnce_EvenWhenPaymentIsReportedAgain()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (_, orderId) = await OrderAsync(factory);

        var first = await SendWebhookAsync(factory, StripeWebhookSigner.CheckoutCompleted($"evt_email_paid_1_{orderId:N}", orderId, OrderTotalInCents));
        var second = await SendWebhookAsync(factory, StripeWebhookSigner.CheckoutCompleted($"evt_email_paid_2_{orderId:N}", orderId, OrderTotalInCents));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal([EmailKind.OrderPaid], await QueuedForAsync(factory, orderId));
    }

    [Fact]
    public async Task Webhook_PaidEventsAtTheSameTime_ThenStripeRetriesTheFailedOnes_OnePaidEmailIsQueued()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (_, orderId) = await OrderAsync(factory);
        var payloads = Enumerable.Range(0, 5)
            .Select(i => StripeWebhookSigner.CheckoutCompleted($"evt_email_parallel_{i}_{orderId:N}", orderId, OrderTotalInCents))
            .ToList();

        var responses = await Task.WhenAll(payloads.Select(p => SendWebhookAsync(factory, p)));
        var retried = payloads.Where((_, i) => responses[i].StatusCode != HttpStatusCode.OK).ToList();
        foreach (var payload in retried)
        {
            Assert.Equal(HttpStatusCode.OK, (await SendWebhookAsync(factory, payload)).StatusCode);
        }

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Equal([EmailKind.OrderPaid], await QueuedForAsync(factory, orderId));
    }

    [Fact]
    public async Task Webhook_Unpaid_QueuesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (_, orderId) = await OrderAsync(factory);

        await SendWebhookAsync(factory, StripeWebhookSigner.CheckoutCompleted($"evt_email_unpaid_{orderId:N}", orderId, OrderTotalInCents, paymentStatus: "unpaid"));

        Assert.Empty(await QueuedForAsync(factory, orderId));
    }

    [Fact]
    public async Task Webhook_WithAnotherAmount_QueuesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (_, orderId) = await OrderAsync(factory);

        await SendWebhookAsync(factory, StripeWebhookSigner.CheckoutCompleted($"evt_email_divergent_{orderId:N}", orderId, OrderTotalInCents - 1));

        Assert.Empty(await QueuedForAsync(factory, orderId));
    }

    [Fact]
    public async Task Ship_QueuesTheShippedEmail()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (_, orderId) = await OrderAsync(factory, order =>
        {
            order.ConfirmPayment("pi_test_ship");
            order.StartPicking();
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IShippingService>().ShipAsync(orderId, "BR123456789BR");
        }

        Assert.Equal([EmailKind.OrderShipped], await QueuedForAsync(factory, orderId));
    }

    [Fact]
    public async Task Ship_RefusedTransition_QueuesNothing()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (_, orderId) = await OrderAsync(factory, order => order.ConfirmPayment("pi_test_ship_refused"));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await Assert.ThrowsAsync<InvalidOrderTransitionException>(
                () => scope.ServiceProvider.GetRequiredService<IShippingService>().ShipAsync(orderId, "BR123456789BR"));
        }

        Assert.Empty(await QueuedForAsync(factory, orderId));
    }

    [Fact]
    public async Task Withdraw_QueuesTheWithdrawalEmail()
    {
        await using var factory = await postgres.CreateMigratedFactoryAsync();
        var (customerId, orderId) = await OrderAsync(factory, order =>
        {
            order.ConfirmPayment("pi_test_withdraw");
            order.StartPicking();
            order.MarkShipped("BR123456789BR");
            order.MarkDelivered(DateTimeOffset.UtcNow);
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IWithdrawalService>().WithdrawAsync(orderId, customerId);
        }

        Assert.Equal([EmailKind.WithdrawalReceived], await QueuedForAsync(factory, orderId));
    }

    [Fact]
    public async Task Refund_OfACancelledOrder_QueuesTheRefundedEmail_ButAWithdrawalRefundDoesNot()
    {
        var stripe = new FakePaymentGateway();
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (_, cancelledId) = await OrderAsync(factory, order =>
        {
            order.ConfirmPayment("pi_test_cancelled");
            order.Cancel();
        });
        var (_, withdrawnId) = await OrderAsync(factory, order =>
        {
            order.ConfirmPayment("pi_test_withdrawn");
            order.StartPicking();
            order.MarkShipped("BR123456789BR");
            order.MarkDelivered(DateTimeOffset.UtcNow);
            order.Withdraw(DateTimeOffset.UtcNow);
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRefundProcessor>().RefundPendingAsync();
        }

        Assert.Equal([EmailKind.OrderRefunded], await QueuedForAsync(factory, cancelledId));
        Assert.Empty(await QueuedForAsync(factory, withdrawnId));
    }

    [Fact]
    public async Task Refund_RefusedByStripe_QueuesNothing()
    {
        var stripe = new FakePaymentGateway { FailRefunds = true };
        await using var factory = await postgres.CreateMigratedFactoryAsync(stripe);
        var (_, orderId) = await OrderAsync(factory, order =>
        {
            order.ConfirmPayment("pi_test_refund_refused");
            order.Cancel();
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRefundProcessor>().RefundPendingAsync();
        }

        Assert.Empty(await QueuedForAsync(factory, orderId));
    }
}
