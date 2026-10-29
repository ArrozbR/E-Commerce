using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Payments;

namespace KeycapStore.IntegrationTests.Payments;

public class StripeCheckoutSessionFactoryTests
{
    private static readonly Uri Return = new("https://loja.test/orders/1/return");

    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 9, 15, 0, 0, TimeSpan.Zero);

    private static Order OrderTo(string state) => new("customer-1", CreatedAt,
    [
        new OrderItem(Guid.NewGuid(), "Kit Aurora (base)", 349.90m, 2),
        new OrderItem(Guid.NewGuid(), "Kit Zéfiro (teclas de função)", 89.90m, 1),
    ], new ShippingAddress("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", state, "80020310"));

    [Fact]
    public void LineItems_AreInBrlCents_AndAddUpToTheOrderTotal()
    {
        var order = OrderTo("PR");

        var options = StripeCheckoutSessionFactory.BuildOptions(order, "cliente@example.com", Return);

        Assert.All(options.LineItems, l => Assert.Equal("brl", l.PriceData.Currency));
        Assert.Equal(34990, options.LineItems[0].PriceData.UnitAmount);
        Assert.Equal(2, options.LineItems[0].Quantity);
        var total = options.LineItems.Sum(l => l.PriceData.UnitAmount!.Value * l.Quantity!.Value);
        Assert.Equal(StripeCheckoutSessionFactory.ToCents(order.Total), total);
        Assert.Equal(80470, total);
    }

    [Fact]
    public void ShippingFee_BecomesALineItem_OnlyWhenItIsNotFree()
    {
        var paid = StripeCheckoutSessionFactory.BuildOptions(OrderTo("PR"), "cliente@example.com", Return);
        var free = StripeCheckoutSessionFactory.BuildOptions(OrderTo("SP"), "cliente@example.com", Return);

        Assert.Equal(1500, Assert.Single(paid.LineItems, l => l.PriceData.ProductData.Name == "Frete").PriceData.UnitAmount);
        Assert.DoesNotContain(free.LineItems, l => l.PriceData.ProductData.Name == "Frete");
    }

    [Fact]
    public void Session_PointsBackToTheOrder()
    {
        var order = OrderTo("PR");

        var options = StripeCheckoutSessionFactory.BuildOptions(order, "cliente@example.com", Return);
        var request = StripeCheckoutSessionFactory.BuildRequestOptions(order);

        Assert.Equal(order.Id.ToString(), options.ClientReferenceId);
        Assert.Equal(order.Id.ToString(), options.Metadata["order_id"]);
        Assert.Equal($"session-{order.Id}", request.IdempotencyKey);
        Assert.Equal("payment", options.Mode);
        Assert.Equal("elements", options.UiMode);
        Assert.Equal(Return.ToString(), options.ReturnUrl);
        Assert.Equal("cliente@example.com", options.CustomerEmail);
        Assert.Null(options.SuccessUrl);
        Assert.Null(options.CancelUrl);
    }

    [Fact]
    public void Expiration_ComesFromTheOrderCreation_SoARetrySendsTheSameData()
    {
        var order = OrderTo("PR");

        var first = StripeCheckoutSessionFactory.BuildOptions(order, "cliente@example.com", Return);
        var retry = StripeCheckoutSessionFactory.BuildOptions(order, "cliente@example.com", Return);

        Assert.Equal(CreatedAt.AddMinutes(31).UtcDateTime, first.ExpiresAt);
        Assert.Equal(first.ExpiresAt, retry.ExpiresAt);
        Assert.Equal(1800, first.PaymentMethodOptions.Pix.ExpiresAfterSeconds);
    }

    [Theory]
    [InlineData("349.90", 34990)]
    [InlineData("89.90", 8990)]
    [InlineData("0.50", 50)]
    [InlineData("1014.70", 101470)]
    public void ToCents_ConvertsReaisToCents(string reais, long cents)
    {
        Assert.Equal(cents, StripeCheckoutSessionFactory.ToCents(decimal.Parse(reais, System.Globalization.CultureInfo.InvariantCulture)));
    }
}
