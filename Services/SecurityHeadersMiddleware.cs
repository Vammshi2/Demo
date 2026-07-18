namespace HostelPro.Services;

public static class SecurityHeadersMiddleware
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app, IConfiguration configuration)
    {
        var allowedImageSources = (configuration["Security:AllowedImageHosts"] ?? string.Empty)
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(host => Uri.CheckHostName(host) != UriHostNameType.Unknown)
            .Select(host => $"https://{host}")
            .Distinct(StringComparer.OrdinalIgnoreCase);
        var imagePolicy = string.Join(' ', new[] { "'self'", "data:" }.Concat(allowedImageSources));

        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.TryAdd("X-Content-Type-Options", "nosniff");
            headers.TryAdd("X-Frame-Options", "DENY");
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
            headers.TryAdd(
                "Content-Security-Policy",
                "default-src 'self'; " +
                $"img-src {imagePolicy}; " +
                "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "script-src 'self'; " +
                "connect-src 'self' wss: ws:; " +
                "frame-ancestors 'none'; base-uri 'self'; form-action 'self'");

            await next();
        });
    }
}
