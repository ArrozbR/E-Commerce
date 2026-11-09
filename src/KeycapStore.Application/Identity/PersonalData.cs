using KeycapStore.Application.Orders;
using KeycapStore.Domain.Orders;

namespace KeycapStore.Application.Identity;

public interface IPersonalDataExport
{
    Task<PersonalData> ExportAsync(string customerId, CancellationToken cancellationToken = default);
}

public sealed record PersonalData(
    DateTimeOffset GeneratedAt,
    PersonalAccount Account,
    ShippingAddress? SavedAddress,
    IReadOnlyList<PersonalOrder> Orders,
    IReadOnlyList<PersonalStockAlert> StockAlerts,
    DateTimeOffset? NewsletterSubscribedAt);

public sealed record PersonalStockAlert(string Product, DateTimeOffset RequestedAt);

public sealed record PersonalAccount(string Email, bool HasPassword, IReadOnlyList<string> ConnectedLogins);

public sealed record PersonalOrder(
    Guid Id,
    DateTimeOffset CreatedAt,
    string Status,
    IReadOnlyList<OrderLineView> Items,
    decimal ShippingFee,
    string? ShippingService,
    decimal Total,
    ShippingAddress ShippingAddress,
    string? TrackingCode,
    string Language);
