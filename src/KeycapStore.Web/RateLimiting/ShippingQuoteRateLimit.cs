using System.ComponentModel.DataAnnotations;
using System.Threading.RateLimiting;

using Microsoft.Extensions.Options;

namespace KeycapStore.Web.RateLimiting;

public sealed class ShippingQuoteLimitOptions
{
    public const string SectionName = "ShippingQuoteLimits";

    [Range(1, 1000)]
    public int PerCustomerPerMinute { get; set; } = 10;

    [Range(1, 10000)]
    public int TotalPerMinute { get; set; } = 100;
}

public sealed class ShippingQuoteLimiter : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly PartitionedRateLimiter<string> limiter;

    public ShippingQuoteLimiter(IOptions<ShippingQuoteLimitOptions> options)
    {
        var limits = options.Value;

        limiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<string, string>(customerId =>
                RateLimitPartition.GetFixedWindowLimiter(customerId, _ => FixedWindow(limits.PerCustomerPerMinute))),
            PartitionedRateLimiter.Create<string, string>(_ =>
                RateLimitPartition.GetFixedWindowLimiter("all-customers", _ => FixedWindow(limits.TotalPerMinute))));
    }

    public bool TryAcquire(string customerId)
    {
        using var lease = limiter.AttemptAcquire(customerId);
        return lease.IsAcquired;
    }

    public void Dispose() => limiter.Dispose();

    private static FixedWindowRateLimiterOptions FixedWindow(int permits) => new()
    {
        PermitLimit = permits,
        Window = Window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}

public static class ShippingQuoteRateLimit
{
    public static IServiceCollection AddShippingQuoteRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ShippingQuoteLimitOptions>()
            .Bind(configuration.GetSection(ShippingQuoteLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<ShippingQuoteLimiter>();

        return services;
    }
}
