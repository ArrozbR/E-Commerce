using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace KeycapStore.Web.RateLimiting;

public sealed class AccountLimitOptions
{
    public const string SectionName = "AccountLimits";

    [Range(1, 1000)]
    public int LoginPerMinute { get; set; } = 10;

    [Range(1, 1000)]
    public int RegisterPerHour { get; set; } = 5;

    [Range(1, 1000)]
    public int ForgotPasswordPer15Minutes { get; set; } = 5;
}

public static class AccountRateLimit
{
    public const string Login = "login";

    public const string Register = "register";

    public const string ForgotPassword = "forgot-password";

    public static IServiceCollection AddAccountRateLimit(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AccountLimitOptions>()
            .Bind(configuration.GetSection(AccountLimitOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<AccountLimitOptions>>((options, limits) =>
            {
                var values = limits.Value;
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.OnRejected = (context, _) =>
                {
                    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    {
                        context.HttpContext.Response.Headers.RetryAfter =
                            ((long)Math.Ceiling(retryAfter.TotalSeconds)).ToString(NumberFormatInfo.InvariantInfo);
                    }

                    return ValueTask.CompletedTask;
                };

                options.AddPolicy(Login, context => ByIp(context, Login, values.LoginPerMinute, TimeSpan.FromMinutes(1)));
                options.AddPolicy(Register, context => ByIp(context, Register, values.RegisterPerHour, TimeSpan.FromHours(1)));
                options.AddPolicy(ForgotPassword, context => ByIp(context, ForgotPassword, values.ForgotPasswordPer15Minutes, TimeSpan.FromMinutes(15)));
            });

        services.AddRateLimiter(_ => { });

        return services;
    }

    private static RateLimitPartition<string> ByIp(HttpContext context, string policy, int permits, TimeSpan window)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter($"{policy}:page");
        }

        var ip = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"{policy}:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true,
        });
    }
}
