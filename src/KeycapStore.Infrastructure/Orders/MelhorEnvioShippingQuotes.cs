using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using KeycapStore.Application.Orders;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KeycapStore.Infrastructure.Orders;

public sealed class MelhorEnvioShippingQuotes(HttpClient http, IOptions<MelhorEnvioOptions> options, ILogger<MelhorEnvioShippingQuotes> logger) : IShippingQuotes
{
    public const string CalculatePath = "api/v2/me/shipment/calculate";

    public async Task<ShippingQuote> QuoteAsync(string destinationPostalCode, IReadOnlyList<ShippingQuoteItem> items, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPostalCode);
        ArgumentOutOfRangeException.ThrowIfZero(items.Count);

        var settings = options.Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.BaseUrl), CalculatePath))
        {
            Content = JsonContent.Create(BuildBody(settings.OriginPostalCode, destinationPostalCode, items)),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.Token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("User-Agent", $"KeycapStore ({settings.ContactEmail})");

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Cotação de frete recusada pelo Melhor Envio: HTTP {StatusCode}.", (int)response.StatusCode);
                return ShippingQuote.Unavailable;
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);

            return new ShippingQuote(true, ReadOptions(json.RootElement));
        }
        catch (Exception error) when (IsUnavailable(error, cancellationToken))
        {
            logger.LogWarning(error, "Cotação de frete falhou no Melhor Envio.");
            return ShippingQuote.Unavailable;
        }
    }

    private static bool IsUnavailable(Exception error, CancellationToken cancellationToken) => error switch
    {
        TaskCanceledException => !cancellationToken.IsCancellationRequested,
        HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException => true,
        _ => false,
    };

    private static object BuildBody(string origin, string destination, IReadOnlyList<ShippingQuoteItem> items) => new
    {
        from = new { postal_code = origin },
        to = new { postal_code = destination },
        products = items.Select(item => new
        {
            id = item.ProductId.ToString(),
            width = item.Package.WidthCm,
            height = item.Package.HeightCm,
            length = item.Package.LengthCm,
            weight = item.Package.WeightKg,
            insurance_value = item.UnitPrice,
            quantity = item.Quantity,
        }),
    };

    private static List<ShippingOption> ReadOptions(JsonElement services)
    {
        var options = new List<ShippingOption>();

        foreach (var service in services.EnumerateArray())
        {
            if (service.TryGetProperty("error", out _) || !service.TryGetProperty("custom_price", out var customPrice))
            {
                continue;
            }

            var price = decimal.Parse(customPrice.GetString()!, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

            options.Add(new ShippingOption(
                service.GetProperty("id").GetInt32(),
                service.GetProperty("company").GetProperty("name").GetString()!,
                service.GetProperty("name").GetString()!,
                price,
                service.GetProperty("custom_delivery_time").GetInt32()));
        }

        return [.. options.OrderBy(option => option.Price)];
    }
}
