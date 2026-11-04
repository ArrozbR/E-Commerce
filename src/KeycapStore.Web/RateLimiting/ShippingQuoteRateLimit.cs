using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.RateLimiting;

using KeycapStore.Web.Localization;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
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

public static class ShippingQuoteRateLimit
{
    public const string PolicyName = "shipping-quote";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    public static IServiceCollection AddShippingQuoteRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ShippingQuoteLimitOptions>()
            .Bind(configuration.GetSection(ShippingQuoteLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous",
                _ => FixedWindow(LimitsOf(context).PerCustomerPerMinute)));

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                IsShippingQuote(context)
                    ? RateLimitPartition.GetFixedWindowLimiter("all-customers", _ => FixedWindow(LimitsOf(context).TotalPerMinute))
                    : RateLimitPartition.GetNoLimiter("not-limited"));

            options.OnRejected = async (rejected, cancellationToken) =>
            {
                var text = rejected.HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();
                rejected.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
                await rejected.HttpContext.Response.WriteAsync(
                    text["Muitas consultas de frete seguidas. Espere um minuto e tente de novo."].Value, cancellationToken);
            };
        });

        return services;
    }

    private static bool IsShippingQuote(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == PolicyName;

    private static ShippingQuoteLimitOptions LimitsOf(HttpContext context) =>
        context.RequestServices.GetRequiredService<IOptions<ShippingQuoteLimitOptions>>().Value;

    private static FixedWindowRateLimiterOptions FixedWindow(int permits) => new()
    {
        PermitLimit = permits,
        Window = Window,
        QueueLimit = 0,
        AutoReplenishment = true,
    };
}
