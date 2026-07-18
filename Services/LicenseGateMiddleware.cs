namespace HostelPro.Services;

public sealed class LicenseGateMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ILicenseService licenseService)
    {
        if (IsInfrastructureRequest(context.Request.Path))
        {
            await next(context);
            return;
        }

        var status = await licenseService.GetStatusAsync(cancellationToken: context.RequestAborted);
        if (status.AllowsAccess || HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "subscription_required",
            message = status.Message
        }, context.RequestAborted);
    }

    private static bool IsInfrastructureRequest(PathString path)
    {
        return path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_blazor")
            || path.StartsWithSegments("/css")
            || path.StartsWithSegments("/images")
            || path.StartsWithSegments("/favicon")
            || path.StartsWithSegments("/health/live");
    }
}

public static class LicenseGateMiddlewareExtensions
{
    public static IApplicationBuilder UseLicenseGate(this IApplicationBuilder app) =>
        app.UseMiddleware<LicenseGateMiddleware>();
}
