using HostelPro.Components;
using HostelPro.Data;
using HostelPro.Models;
using HostelPro.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Resend;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is required. Configure it with user-secrets or a hosting environment variable.");
}
var dataDirectory = GetDataDirectory(builder.Environment, builder.Configuration);
var dataProtectionKeyDirectory = builder.Configuration["DataProtection:KeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeyDirectory))
{
    dataProtectionKeyDirectory = Path.Combine(dataDirectory, "DataProtectionKeys");
}

void ConfigureDatabase(DbContextOptionsBuilder options) =>
    options.UseNpgsql(connectionString, npgsql =>
        npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null));

builder.Services.AddDbContextFactory<ApplicationDbContext>(ConfigureDatabase);
builder.Services.AddTransient(serviceProvider =>
    serviceProvider.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 10;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "HostelPro.Dev"
        : "__Host-HostelPro";
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.LoginPath = "/Login";
    options.AccessDeniedPath = "/AccessDenied";
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(SecurityPolicies.AdminOnly, policy =>
        policy.RequireAuthenticatedUser().RequireRole(AppRoles.Admin));
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyDirectory));

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddTransient<IHostelRepository, HostelRepository>();
builder.Services.AddScoped<IHostelSettingsReader, HostelSettingsReader>();
builder.Services.Configure<LicensingOptions>(builder.Configuration.GetSection(LicensingOptions.SectionName));
builder.Services.Configure<ProvisioningOptions>(builder.Configuration.GetSection(ProvisioningOptions.SectionName));
builder.Services.AddSingleton<ISetupTokenValidator, SetupTokenValidator>();
builder.Services.AddHttpClient("HostelProLicense", client =>
{
    client.Timeout = TimeSpan.FromSeconds(12);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HostelPro/1.0");
});
builder.Services.AddSingleton<ILicenseService, LicenseService>();
builder.Services.AddScoped<IPaymentQrService, PaymentQrService>();
builder.Services.AddScoped<IReceiptPdfService, ReceiptPdfService>();
builder.Services.AddScoped<IReportPdfService, ReportPdfService>();
builder.Services.AddScoped<IKycFileStorage, KycFileStorage>();
builder.Services.AddScoped<IPublicImageStorage, PublicImageStorage>();
builder.Services.Configure<EmailDeliveryOptions>(builder.Configuration.GetSection(EmailDeliveryOptions.SectionName));
builder.Services.AddResend(options =>
{
    options.ApiToken = builder.Configuration["Resend:ApiToken"] ?? string.Empty;
});
builder.Services.AddScoped<IEmailNotificationService, ResendEmailNotificationService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    Directory.CreateDirectory(dataDirectory);

    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseSecurityHeaders(app.Configuration);
app.UseStaticFiles();
app.UseRouting();
app.UseLicenseGate();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapPost("/account/login", async (
    HttpContext httpContext,
    IAntiforgery antiforgery,
    SignInManager<ApplicationUser> signInManager,
    [FromForm] LoginRequest request) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
    {
        return Results.Redirect("/Login?error=token");
    }

    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.Redirect("/Login?error=missing");
    }

    var user = await signInManager.UserManager.FindByEmailAsync(request.Email.Trim());
    if (user is null || !user.IsActive)
    {
        return Results.Redirect("/Login?error=inactive");
    }

    var result = await signInManager.PasswordSignInAsync(
        user,
        request.Password,
        request.RememberMe,
        lockoutOnFailure: true);

    if (!result.Succeeded)
    {
        return Results.Redirect("/Login?error=invalid");
    }

    user.LastLoginUtc = DateTime.UtcNow;
    await signInManager.UserManager.UpdateAsync(user);

    var returnUrl = httpContext.Request.Query["returnUrl"].ToString();
    var safeReturnUrl = IsLocalReturnUrl(returnUrl) ? returnUrl : "/admin/Dashboard";
    return Results.Redirect(safeReturnUrl);
}).DisableAntiforgery();

app.MapPost("/account/register", async (
    HttpContext httpContext,
    IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailNotificationService emailNotifications,
    IHostelSettingsReader settingsReader,
    [FromForm] RegisterRequest request) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
    {
        return Results.Redirect("/Register?error=token");
    }

    var settings = await settingsReader.GetSettingsAsync(httpContext.RequestAborted);
    if (!settings.PublicRegistrationEnabled)
    {
        return Results.Redirect("/Register?error=closed");
    }

    if (request.Password != request.ConfirmPassword || request.Password.Length < 10)
    {
        return Results.Redirect("/Register?error=password");
    }

    var user = new ApplicationUser
    {
        UserName = request.Email.Trim(),
        Email = request.Email.Trim(),
        FullName = request.FullName.Trim(),
        PhoneNumber = request.Phone?.Trim(),
        EmailConfirmed = false
    };

    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        return Results.Redirect("/Register?error=exists");
    }

    await userManager.AddToRoleAsync(user, AppRoles.User);
    await signInManager.SignInAsync(user, isPersistent: false);
    await emailNotifications.SendRegistrationAsync(user);
    return Results.Redirect("/rooms");
}).DisableAntiforgery();

app.MapPost("/account/logout", async (
    HttpContext httpContext,
    IAntiforgery antiforgery,
    SignInManager<ApplicationUser> signInManager) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
    {
        return Results.Redirect("/");
    }

    await signInManager.SignOutAsync();
    return Results.Redirect("/");
}).RequireAuthorization().DisableAntiforgery();

app.MapPost("/api/payments/webhook", async (
    HttpContext httpContext,
    IHostelRepository repository,
    IConfiguration configuration) =>
{
    var secret = configuration["PaymentGateway:WebhookSecret"];
    if (string.IsNullOrWhiteSpace(secret)) return Results.NotFound();

    using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8);
    var body = await reader.ReadToEndAsync(httpContext.RequestAborted);
    var signature = httpContext.Request.Headers["X-HostelPro-Signature"].ToString();
    if (!ValidWebhookSignature(body, signature, secret)) return Results.Unauthorized();

    PaymentWebhookRequest? request;
    try
    {
        request = JsonSerializer.Deserialize<PaymentWebhookRequest>(body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }
    catch (JsonException)
    {
        return Results.BadRequest(new { error = "Invalid webhook payload." });
    }

    if (request is null) return Results.BadRequest(new { error = "Invalid webhook payload." });
    try
    {
        var payment = await repository.RecordGatewayPaymentAsync(request);
        return Results.Ok(new { received = true, receiptNumber = payment.ReceiptNumber });
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
}).DisableAntiforgery();

app.MapPost("/setup/complete", async (
    HttpContext httpContext,
    IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext db,
    ISetupTokenValidator setupTokenValidator,
    [FromForm] SetupRequest request) =>
{
    if (!await IsAntiforgeryValidAsync(httpContext, antiforgery))
    {
        return Results.Redirect(SetupRedirect("request", request.SetupToken));
    }

    if ((await userManager.GetUsersInRoleAsync(AppRoles.Admin)).Count > 0)
    {
        return Results.Redirect("/Setup?error=closed");
    }

    if (!setupTokenValidator.Validate(request.SetupToken))
    {
        return Results.Redirect("/Setup?error=activation");
    }

    if (request.Password != request.ConfirmPassword || request.Password.Length < 10)
    {
        return Results.Redirect(SetupRedirect("password", request.SetupToken));
    }

    var owner = new ApplicationUser
    {
        UserName = request.Email.Trim(),
        Email = request.Email.Trim(),
        FullName = request.OwnerName.Trim(),
        PhoneNumber = request.Phone?.Trim(),
        EmailConfirmed = true,
        IsActive = true
    };
    var createResult = await userManager.CreateAsync(owner, request.Password);
    if (!createResult.Succeeded)
    {
        return Results.Redirect(SetupRedirect("account", request.SetupToken));
    }

    var roleResult = await userManager.AddToRoleAsync(owner, AppRoles.Admin);
    if (!roleResult.Succeeded)
    {
        await userManager.DeleteAsync(owner);
        return Results.Redirect(SetupRedirect("account", request.SetupToken));
    }

    var setting = await db.HostelSettings.OrderBy(item => item.Id).FirstAsync(httpContext.RequestAborted);
    setting.HostelName = request.PropertyName.Trim();
    setting.UpiPayeeName = request.PropertyName.Trim();
    setting.ContactEmail = request.Email.Trim();
    setting.ContactPhone = request.Phone?.Trim() ?? string.Empty;
    setting.WhatsAppPhone = request.Phone?.Trim() ?? string.Empty;
    await db.SaveChangesAsync(httpContext.RequestAborted);
    await signInManager.SignInAsync(owner, isPersistent: false);
    return Results.Redirect("/admin/Settings");
}).DisableAntiforgery();

app.MapGet("/admin/documents/{id:guid}", async (
    Guid id,
    ApplicationDbContext db,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    ILicenseService licenseService,
    CancellationToken cancellationToken) =>
{
    var license = await licenseService.GetStatusAsync(cancellationToken: cancellationToken);
    if (!license.AllowsAccess)
    {
        return Results.StatusCode(StatusCodes.Status402PaymentRequired);
    }

    var document = await db.TenantDocuments
        .AsNoTracking()
        .Include(item => item.Tenant)
        .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    if (document is null || string.IsNullOrWhiteSpace(document.FileUrl))
    {
        return Results.NotFound();
    }

    var fileName = Path.GetFileName(document.FileUrl);
    var filePath = Path.Combine(GetDataDirectory(environment, configuration), "Uploads", "Kyc", fileName);
    if (!System.IO.File.Exists(filePath))
    {
        return Results.NotFound();
    }

    var downloadName = $"{SafeFileName(document.Tenant?.FullName ?? "tenant")}-{SafeFileName(document.DocumentType)}{Path.GetExtension(fileName)}";
    return Results.File(filePath, "application/octet-stream", downloadName);
}).RequireAuthorization(SecurityPolicies.AdminOnly);

var reportEndpoints = app.MapGroup("/admin/reports")
    .RequireAuthorization(SecurityPolicies.AdminOnly);

reportEndpoints.MapGet("/tenants.pdf", async (
    HttpContext httpContext,
    IHostelRepository repository,
    IReportPdfService pdf,
    ILicenseService licenseService,
    CancellationToken cancellationToken) =>
{
    var license = await licenseService.GetStatusAsync(cancellationToken: cancellationToken);
    if (!license.AllowsAccess) return Results.StatusCode(StatusCodes.Status402PaymentRequired);
    var bytes = pdf.BuildTenantList(await repository.GetTenantsAsync(), await repository.GetSettingsAsync());
    httpContext.Response.Headers.ContentDisposition = $"inline; filename=tenant-list-{DateTime.Today:yyyyMMdd}.pdf";
    return Results.File(bytes, "application/pdf");
});

reportEndpoints.MapGet("/payments.pdf", async (
    HttpContext httpContext,
    IHostelRepository repository,
    IReportPdfService pdf,
    ILicenseService licenseService,
    CancellationToken cancellationToken) =>
{
    var license = await licenseService.GetStatusAsync(cancellationToken: cancellationToken);
    if (!license.AllowsAccess) return Results.StatusCode(StatusCodes.Status402PaymentRequired);
    var bytes = pdf.BuildPaymentList(await repository.GetPaymentsAsync(), await repository.GetSettingsAsync());
    httpContext.Response.Headers.ContentDisposition = $"inline; filename=paid-payments-{DateTime.Today:yyyyMMdd}.pdf";
    return Results.File(bytes, "application/pdf");
});

reportEndpoints.MapGet("/unpaid.pdf", async (
    HttpContext httpContext,
    IHostelRepository repository,
    IReportPdfService pdf,
    ILicenseService licenseService,
    CancellationToken cancellationToken) =>
{
    var license = await licenseService.GetStatusAsync(cancellationToken: cancellationToken);
    if (!license.AllowsAccess) return Results.StatusCode(StatusCodes.Status402PaymentRequired);
    var bytes = pdf.BuildUnpaidList(await repository.GetBillsAsync(), await repository.GetSettingsAsync());
    httpContext.Response.Headers.ContentDisposition = $"inline; filename=unpaid-report-{DateTime.Today:yyyyMMdd}.pdf";
    return Results.File(bytes, "application/pdf");
});

app.MapGet("/health/live", () => Results.Ok(new
{
    status = "healthy",
    utc = DateTime.UtcNow
}));

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static bool IsLocalReturnUrl(string? returnUrl)
{
    return !string.IsNullOrWhiteSpace(returnUrl)
        && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        && !returnUrl.StartsWith("//", StringComparison.Ordinal);
}

static bool ValidWebhookSignature(string payload, string signature, string secret)
{
    if (string.IsNullOrWhiteSpace(signature)) return false;
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    var provided = signature.Trim().ToLowerInvariant();
    return expected.Length == provided.Length
        && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(provided));
}

static async Task<bool> IsAntiforgeryValidAsync(HttpContext httpContext, IAntiforgery antiforgery)
{
    try
    {
        await antiforgery.ValidateRequestAsync(httpContext);
        return true;
    }
    catch (AntiforgeryValidationException)
    {
        return false;
    }
}

static string SafeFileName(string value)
{
    var invalid = Path.GetInvalidFileNameChars();
    var cleaned = new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
    return string.IsNullOrWhiteSpace(cleaned) ? "document" : cleaned.Trim();
}

static string GetDataDirectory(IWebHostEnvironment environment, IConfiguration configuration)
{
    var configuredPath = configuration["Storage:DataPath"];
    return string.IsNullOrWhiteSpace(configuredPath)
        ? Path.Combine(environment.ContentRootPath, "App_Data")
        : configuredPath;
}

static string SetupRedirect(string error, string? setupToken)
{
    var token = Uri.EscapeDataString(setupToken?.Trim() ?? string.Empty);
    return $"/Setup?error={Uri.EscapeDataString(error)}&token={token}";
}

public sealed record LoginRequest(string Email, string Password, bool RememberMe);

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string? Phone,
    string Password,
    string ConfirmPassword);

public sealed record SetupRequest(
    string SetupToken,
    string PropertyName,
    string OwnerName,
    string Email,
    string? Phone,
    string Password,
    string ConfirmPassword);
