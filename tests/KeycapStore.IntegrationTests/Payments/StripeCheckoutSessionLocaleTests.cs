using KeycapStore.Domain.Orders;
using KeycapStore.Infrastructure.Payments;

namespace KeycapStore.IntegrationTests.Payments;

public class StripeCheckoutSessionLocaleTests
{
    private static readonly Uri Return = new("https://loja.test/orders/1/return");

    private static Order OrderIn(string language) => new("customer-1", DateTimeOffset.UtcNow,
        [new OrderItem(Guid.NewGuid(), "Kit Aurora (base)", 349.90m, 1)],
        new ShippingAddress("Bruno Lima", "Rua XV de Novembro", "200", null, "Centro", "Curitiba", "PR", "80020310"),
        language);

    [Fact]
    public void OrderInPortuguese_OpensStripeInPortuguese()
    {
        var options = StripeCheckoutSessionFactory.BuildOptions(OrderIn("pt-BR"), "cliente@example.com", Return);

        Assert.Equal("pt-BR", options.Locale);
        Assert.Contains(options.LineItems, l => l.PriceData.ProductData.Name == "Frete");
    }

    [Fact]
    public void OrderInEnglish_OpensStripeInEnglish()
    {
        var options = StripeCheckoutSessionFactory.BuildOptions(OrderIn("en-US"), "cliente@example.com", Return);

        Assert.Equal("en", options.Locale);
        Assert.Contains(options.LineItems, l => l.PriceData.ProductData.Name == "Shipping");
    }

    [Fact]
    public void RetryOfTheSameOrder_UsesTheSameLocale()
    {
        var order = OrderIn("en-US");

        var first = StripeCheckoutSessionFactory.BuildOptions(order, "cliente@example.com", Return);
        var retry = StripeCheckoutSessionFactory.BuildOptions(order, "cliente@example.com", Return);

        Assert.Equal(first.Locale, retry.Locale);
    }
}
