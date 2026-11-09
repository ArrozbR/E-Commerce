using KeycapStore.Application.Notifications;
using KeycapStore.Domain.Orders;
using KeycapStore.IntegrationTests.Fixtures;

using Microsoft.AspNetCore.Mvc.Testing;

using static KeycapStore.IntegrationTests.Fixtures.TestEmails;

namespace KeycapStore.IntegrationTests.Notifications;

public class OrderEmailContentTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static async Task<(EmailMessage Message, Guid OrderId)> SendAsync(
        WebApplicationFactory<Program> factory,
        FakeEmailSender sender,
        EmailKind kind,
        Action<Order>? change = null,
        string language = Order.DefaultLanguage,
        DateTimeOffset? preOrderShipsAt = null)
    {
        var (customerId, address) = await CreateCustomerAsync(factory);
        var orderId = await SaveOrderAsync(factory, customerId, change, language, preOrderShipsAt);
        await QueueAsync(factory, kind, orderId);

        await DispatchAsync(factory);

        return (Assert.Single(sender.Sent, m => m.To == address), orderId);
    }

    private static string Plain(string text) => text.Replace('\u00A0', ' ').Replace('\u202F', ' ');

    [Fact]
    public async Task Paid_InPortuguese_ListsTheItemsShippingAndTotal_WithALinkToTheOrder()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);

        var (message, orderId) = await SendAsync(factory, sender, EmailKind.OrderPaid, order => order.ConfirmPayment("pi_test_content"));

        Assert.Equal($"Pagamento confirmado: pedido {orderId.ToString()[..8]}", message.Subject);
        Assert.Contains("1 × Kit Aurora (base): R$ 349,90", Plain(message.TextBody));
        Assert.Contains("Frete (Correios PAC): R$ 15,00", Plain(message.TextBody));
        Assert.Contains("Total: R$ 364,90", Plain(message.TextBody));
        Assert.Contains("código de rastreio", Plain(message.TextBody));
        Assert.Contains($"https://localhost:7290/Orders/Details/{orderId}", Plain(message.TextBody));
        Assert.Contains($"href=\"https://localhost:7290/Orders/Details/{orderId}\"", message.HtmlBody);
    }

    [Fact]
    public async Task Paid_EnglishOrder_IsWrittenInEnglish_WithTheRealSymbol()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);

        var (message, orderId) = await SendAsync(factory, sender, EmailKind.OrderPaid, order => order.ConfirmPayment("pi_test_content_en"), language: "en-US");

        Assert.Equal($"Payment confirmed: order {orderId.ToString()[..8]}", message.Subject);
        Assert.Contains("Total: R$364.90", Plain(message.TextBody));
        Assert.Contains("View the order", message.HtmlBody);
    }

    [Fact]
    public async Task Paid_PreOrder_TellsTheExpectedShippingDateInBrasilia()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);
        var shipsAt = new DateTimeOffset(2027, 1, 20, 2, 0, 0, TimeSpan.Zero);

        var (message, _) = await SendAsync(factory, sender, EmailKind.OrderPaid, order => order.ConfirmPayment("pi_test_content_pre"), preOrderShipsAt: shipsAt);

        Assert.Contains("envio está previsto para 19/01/2027", Plain(message.TextBody));
    }

    [Fact]
    public async Task Shipped_HasTheServiceAndTheTrackingCode()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);

        var (message, _) = await SendAsync(factory, sender, EmailKind.OrderShipped, order =>
        {
            order.ConfirmPayment("pi_test_content_ship");
            order.StartPicking();
            order.MarkShipped("BR987654321BR");
        });

        Assert.Contains("foi enviado por Correios PAC", Plain(message.TextBody));
        Assert.Contains("Código de rastreio: BR987654321BR", Plain(message.TextBody));
    }

    [Fact]
    public async Task Refunded_HasTheRefundedAmount()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);

        var (message, _) = await SendAsync(factory, sender, EmailKind.OrderRefunded, order =>
        {
            order.ConfirmPayment("pi_test_content_refund");
            order.Cancel();
            order.MarkRefunded();
        });

        Assert.Contains("foi cancelado", Plain(message.TextBody));
        Assert.Contains("O valor de R$ 364,90 foi devolvido", Plain(message.TextBody));
    }

    [Fact]
    public async Task Withdrawal_ConfirmsTheRequest_AndTellsWhatHappensNext()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);

        var (message, _) = await SendAsync(factory, sender, EmailKind.WithdrawalReceived, order =>
        {
            order.ConfirmPayment("pi_test_content_withdraw");
            order.StartPicking();
            order.MarkShipped("BR123456789BR");
            order.MarkDelivered(DateTimeOffset.UtcNow);
            order.Withdraw(DateTimeOffset.UtcNow);
        });

        Assert.Contains("Recebemos a sua desistência", Plain(message.TextBody));
        Assert.Contains("O reembolso de R$ 364,90 é feito automaticamente", Plain(message.TextBody));
        Assert.Contains("comprovante", Plain(message.TextBody));
    }

    [Fact]
    public async Task Html_EscapesTextThatComesFromTheDatabase()
    {
        var sender = new FakeEmailSender();
        await using var factory = await postgres.CreateMigratedFactoryAsync(sender);

        var (message, _) = await SendAsync(factory, sender, EmailKind.OrderShipped, order =>
        {
            order.ConfirmPayment("pi_test_content_html");
            order.StartPicking();
            order.MarkShipped("<b>BR1</b>");
        });

        Assert.Contains("&lt;b&gt;BR1&lt;/b&gt;", message.HtmlBody);
        Assert.DoesNotContain("<b>BR1</b>", message.HtmlBody);
    }
}
