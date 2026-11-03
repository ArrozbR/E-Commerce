using KeycapStore.Domain.Catalog;

namespace KeycapStore.Application.Orders;

public sealed record ShippingQuoteItem(Guid ProductId, ProductPackage Package, decimal UnitPrice, int Quantity);

public sealed record ShippingOption(int ServiceId, string Carrier, string Service, decimal Price, int DeliveryDays);

public sealed record ShippingQuote(bool Available, IReadOnlyList<ShippingOption> Options)
{
    public static ShippingQuote Unavailable { get; } = new(false, []);
}

public interface IShippingQuotes
{
    Task<ShippingQuote> QuoteAsync(string destinationPostalCode, IReadOnlyList<ShippingQuoteItem> items, CancellationToken cancellationToken = default);
}
