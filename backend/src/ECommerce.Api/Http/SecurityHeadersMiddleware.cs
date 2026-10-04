namespace ECommerce.Api.Http;

internal static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (!context.Request.Path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase))
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            await next();
        });
}
