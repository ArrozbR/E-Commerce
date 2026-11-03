using KeycapStore.Application.Orders;

namespace KeycapStore.IntegrationTests.Fixtures;

public sealed class FakeShippingQuotes : IShippingQuotes
{
    private readonly Lock gate = new();
    private readonly List<(string PostalCode, IReadOnlyList<ShippingQuoteItem> Items)> requests = [];

    public List<ShippingOption> Options { get; } =
    [
        new(TestShipping.PacServiceId, "Correios", "PAC", 15.00m, 7),
        new(2, "Correios", "SEDEX", 45.00m, 3),
    ];

    public bool Unavailable { get; set; }

    public IReadOnlyList<(string PostalCode, IReadOnlyList<ShippingQuoteItem> Items)> Requests
    {
        get
        {
            lock (gate)
            {
                return [.. requests];
            }
        }
    }

    public Task<ShippingQuote> QuoteAsync(string destinationPostalCode, IReadOnlyList<ShippingQuoteItem> items, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            requests.Add((destinationPostalCode, items));
            return Task.FromResult(Unavailable ? ShippingQuote.Unavailable : new ShippingQuote(true, [.. Options]));
        }
    }
}
