namespace KeycapStore.Web.Security;

public static class SecurityHeaders
{
    public static readonly string ContentSecurityPolicy = string.Join("; ",
        "default-src 'self'",
        "script-src 'self' https://js.stripe.com https://*.js.stripe.com",
        "style-src 'self' 'unsafe-inline'",
        "img-src 'self' data: https://*.stripe.com https://*.link.com",
        "font-src 'self'",
        "connect-src 'self' https://api.stripe.com https://link.com https://*.link.com",
        "frame-src https://js.stripe.com https://*.js.stripe.com https://hooks.stripe.com https://link.com https://*.link.com",
        "form-action 'self' https://accounts.google.com",
        "frame-ancestors 'none'",
        "base-uri 'self'",
        "object-src 'none'");

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.ContentSecurityPolicy = ContentSecurityPolicy;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

            await next(context);
        });
}
