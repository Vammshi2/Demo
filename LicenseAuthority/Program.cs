using System.ComponentModel.DataAnnotations;
using System.Net.Http.Headers;
using System.Threading.RateLimiting;
using HostelPro.LicenseAuthority.Contracts;
using HostelPro.LicenseAuthority.Data;
using HostelPro.LicenseAuthority.Domain;
using HostelPro.LicenseAuthority.Options;
using HostelPro.LicenseAuthority.Security;
using HostelPro.LicenseAuthority.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var databaseProvider = builder.Configuration["AuthorityDatabase:Provider"]?.Trim().ToLowerInvariant()
    ?? "postgresql";
var useDevelopmentSqlite = databaseProvider == "sqlite" && builder.Environment.IsDevelopment();
var connectionString = builder.Configuration.GetConnectionString("LicenseAuthority");
if (useDevelopmentSqlite && string.IsNullOrWhiteSpace(connectionString))
{
    var dataDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
    Directory.CreateDirectory(dataDirectory);
    connectionString = $"Data Source={Path.Combine(dataDirectory, "license-authority-dev.db")}";
}

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:LicenseAuthority is required. Configure a separate authority database with user-secrets or an environment variable.");
}

builder.Services.AddDbContext<LicenseAuthorityDbContext>(options =>
{
    if (useDevelopmentSqlite)
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(connectionString);
    }
});
builder.Services.Configure<AdminSetupOptions>(
    builder.Configuration.GetSection(AdminSetupOptions.SectionName));
builder.Services.Configure<LicenseValidationOptions>(
    builder.Configuration.GetSection(LicenseValidationOptions.SectionName));
builder.Services.Configure<JsonOptions>(options =>
    options.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("HostelPro.LicenseAuthority");
var keyPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keyPath))
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
}

builder.Services.AddAuthentication(VendorAuthenticationDefaults.Scheme)
    .AddCookie(VendorAuthenticationDefaults.Scheme, options =>
    {
        options.Cookie.Name = builder.Environment.IsDevelopment()
            ? "HostelProVendor.Dev"
            : "__Host-HostelProVendor";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin");
    options.Conventions.AuthorizePage("/Logout");
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => FixedWindow(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        permitLimit: 10,
        window: TimeSpan.FromMinutes(1)));
    options.AddPolicy("validation", context => FixedWindow(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        permitLimit: 120,
        window: TimeSpan.FromMinutes(1)));
});
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ILicenseKeyService, LicenseKeyService>();
builder.Services.AddScoped<ILicenseValidationService, LicenseValidationService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<VendorAdmin>,
    Microsoft.AspNetCore.Identity.PasswordHasher<VendorAdmin>>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers.XFrameOptions = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers.ContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'; img-src 'self' data:";
    headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/v1/licenses/validate", async (
        HttpContext context,
        LicenseValidationRequest request,
        ILicenseValidationService validationService,
        CancellationToken cancellationToken) =>
    {
        context.Response.Headers.CacheControl = "no-store";
        if (context.Request.ContentLength is > 16_384)
        {
            return Results.Problem(statusCode: StatusCodes.Status413PayloadTooLarge);
        }

        var validationErrors = ValidateRequest(request);
        if (validationErrors.Count > 0)
        {
            return Results.ValidationProblem(validationErrors);
        }

        if (!AuthenticationHeaderValue.TryParse(context.Request.Headers.Authorization, out var authorization)
            || !authorization.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(authorization.Parameter))
        {
            return Results.Json(
                new { error = "A bearer license key is required." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var result = await validationService.ValidateAsync(
            authorization.Parameter,
            request,
            cancellationToken);
        return result.KeyRecognized
            ? Results.Ok(result.Response)
            : Results.Json(
                new { error = "The license key is invalid." },
                statusCode: StatusCodes.Status401Unauthorized);
    })
    .AllowAnonymous()
    .DisableAntiforgery()
    .RequireRateLimiting("validation");

app.MapRazorPages();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<LicenseAuthorityDbContext>();
    if (useDevelopmentSqlite)
    {
        await dbContext.Database.EnsureCreatedAsync();
    }
    else
    {
        await dbContext.Database.MigrateAsync();
    }
}

await app.RunAsync();

static RateLimitPartition<string> FixedWindow(string partitionKey, int permitLimit, TimeSpan window) =>
    RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = window,
        QueueLimit = 0,
        AutoReplenishment = true
    });

static Dictionary<string, string[]> ValidateRequest(LicenseValidationRequest request)
{
    var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), true);

    AddRequired(errors, nameof(request.ProductCode), request.ProductCode, 80);
    AddRequired(errors, nameof(request.InstallationId), request.InstallationId, 80);
    AddRequired(errors, nameof(request.ApplicationVersion), request.ApplicationVersion, 40);
    AddRequired(errors, nameof(request.HostName), request.HostName, 255);
    if (!string.IsNullOrWhiteSpace(request.InstallationId) && !Guid.TryParse(request.InstallationId, out _))
    {
        errors[nameof(request.InstallationId)] = ["InstallationId must be a GUID."];
    }

    return errors;
}

static void AddRequired(
    IDictionary<string, string[]> errors,
    string property,
    string? value,
    int maximumLength)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        errors[property] = ["The field is required."];
    }
    else if (value.Trim().Length > maximumLength)
    {
        errors[property] = [$"The field cannot exceed {maximumLength} characters."];
    }
}

public partial class Program;
