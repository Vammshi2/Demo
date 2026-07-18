using System.ComponentModel.DataAnnotations;
using HostelPro.LicenseAuthority.Data;
using HostelPro.LicenseAuthority.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;

namespace HostelPro.LicenseAuthority.Pages.Admin;

[Authorize]
public sealed class EditModel(
    LicenseAuthorityDbContext dbContext,
    TimeProvider timeProvider,
    IConfiguration configuration) : PageModel
{
    [BindProperty]
    public Guid Id { get; set; }

    [BindProperty]
    public LicenseInput Input { get; set; } = new();

    [BindProperty]
    public DeploymentInput Deployment { get; set; } = new();

    public string KeyPrefix { get; private set; } = string.Empty;
    public string SuccessMessage { get; private set; } = string.Empty;
    public IReadOnlyList<InstallationRow> Installations { get; private set; } = [];
    public string DeploymentTemplate { get; private set; } = string.Empty;
    public string PublicApplicationUrl { get; private set; } = string.Empty;
    public string OwnerSetupUrl { get; private set; } = string.Empty;
    public string AdminLoginUrl { get; private set; } = string.Empty;
    public string TenantApplicationUrl { get; private set; } = string.Empty;
    public bool HasApplicationLinks => !string.IsNullOrWhiteSpace(PublicApplicationUrl);

    [TempData]
    public string IssuedSetupToken { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var license = await dbContext.Licenses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (license is null)
        {
            return NotFound();
        }

        Id = license.Id;
        KeyPrefix = license.KeyPrefix;
        Input = new LicenseInput
        {
            CustomerName = license.CustomerName,
            ProductCode = license.ProductCode,
            Status = license.Status,
            PaidThroughUtc = license.PaidThroughUtc.UtcDateTime,
            MaxInstallations = license.MaxInstallations,
            Notes = license.Notes
        };
        Deployment = DeploymentInput.FromLicense(license);
        BuildDeploymentTemplate(license, IssuedSetupToken);
        BuildApplicationLinks(license.ApplicationUrl, IssuedSetupToken);
        await LoadInstallationsAsync(id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken cancellationToken)
    {
        Id = id;
        var license = await dbContext.Licenses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (license is null)
        {
            return NotFound();
        }

        KeyPrefix = license.KeyPrefix;
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
        {
            Deployment = DeploymentInput.FromLicense(license);
            BuildDeploymentTemplate(license, string.Empty);
            BuildApplicationLinks(license.ApplicationUrl, string.Empty);
            await LoadInstallationsAsync(id, cancellationToken);
            return Page();
        }

        license.CustomerName = Input.CustomerName.Trim();
        license.ProductCode = Input.ProductCode.Trim().ToLowerInvariant();
        license.Status = Input.Status;
        license.PaidThroughUtc = new DateTimeOffset(DateTime.SpecifyKind(Input.PaidThroughUtc, DateTimeKind.Utc));
        license.MaxInstallations = Input.MaxInstallations;
        license.Notes = Input.Notes.Trim();
        license.UpdatedUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        SuccessMessage = "License changes saved.";
        Deployment = DeploymentInput.FromLicense(license);
        BuildDeploymentTemplate(license, string.Empty);
        BuildApplicationLinks(license.ApplicationUrl, string.Empty);
        await LoadInstallationsAsync(id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveDeploymentAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        Id = id;
        var license = await dbContext.Licenses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (license is null)
        {
            return NotFound();
        }

        KeyPrefix = license.KeyPrefix;
        Input = new LicenseInput
        {
            CustomerName = license.CustomerName,
            ProductCode = license.ProductCode,
            Status = license.Status,
            PaidThroughUtc = license.PaidThroughUtc.UtcDateTime,
            MaxInstallations = license.MaxInstallations,
            Notes = license.Notes
        };
        ModelState.Clear();
        if (!string.IsNullOrWhiteSpace(Deployment.ApplicationUrl)
            && (!Uri.TryCreate(Deployment.ApplicationUrl.Trim(), UriKind.Absolute, out var applicationUri)
                || (applicationUri.Scheme != Uri.UriSchemeHttps && applicationUri.Scheme != Uri.UriSchemeHttp)))
        {
            ModelState.AddModelError(
                $"{nameof(Deployment)}.{nameof(Deployment.ApplicationUrl)}",
                "Enter an absolute HTTP or HTTPS application URL.");
        }
        if (string.IsNullOrWhiteSpace(Deployment.HostingProvider))
        {
            ModelState.AddModelError(
                $"{nameof(Deployment)}.{nameof(Deployment.HostingProvider)}",
                "Select a hosting provider.");
        }
        if (Deployment.DeploymentStatus is not ("not_configured" or "ready" or "deploying" or "deployed" or "failed"))
        {
            ModelState.AddModelError(
                $"{nameof(Deployment)}.{nameof(Deployment.DeploymentStatus)}",
                "Select a valid deployment status.");
        }
        if (!ModelState.IsValid)
        {
            BuildDeploymentTemplate(license, string.Empty);
            BuildApplicationLinks(Deployment.ApplicationUrl, string.Empty);
            await LoadInstallationsAsync(id, cancellationToken);
            return Page();
        }

        license.ApplicationUrl = (Deployment.ApplicationUrl ?? string.Empty).Trim();
        license.HostingProvider = (Deployment.HostingProvider ?? string.Empty).Trim().ToLowerInvariant();
        license.DeploymentRegion = (Deployment.DeploymentRegion ?? string.Empty).Trim();
        license.DeploymentStatus = Deployment.DeploymentStatus;
        license.SecretReference = (Deployment.SecretReference ?? string.Empty).Trim();
        if (Deployment.DeploymentStatus == "deployed" && license.LastDeployedUtc is null)
        {
            license.LastDeployedUtc = timeProvider.GetUtcNow();
        }
        license.UpdatedUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        SuccessMessage = "Deployment profile saved.";
        BuildDeploymentTemplate(license, string.Empty);
        BuildApplicationLinks(license.ApplicationUrl, string.Empty);
        await LoadInstallationsAsync(id, cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeInstallationAsync(
        Guid id,
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var installation = await dbContext.Installations.SingleOrDefaultAsync(
            x => x.Id == installationId && x.LicenseId == id,
            cancellationToken);
        if (installation is null)
        {
            return NotFound();
        }

        installation.RevokedUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRestoreInstallationAsync(
        Guid id,
        Guid installationId,
        CancellationToken cancellationToken)
    {
        var license = await dbContext.Licenses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        var installation = await dbContext.Installations.SingleOrDefaultAsync(
            x => x.Id == installationId && x.LicenseId == id,
            cancellationToken);
        if (license is null || installation is null)
        {
            return NotFound();
        }

        var activeCount = await dbContext.Installations.CountAsync(
            x => x.LicenseId == id && x.RevokedUtc == null,
            cancellationToken);
        if (activeCount >= license.MaxInstallations)
        {
            TempData["InstallError"] = "Increase the installation limit or revoke another installation first.";
            return RedirectToPage(new { id });
        }

        installation.RevokedUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostGenerateSetupTokenAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await dbContext.Licenses.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken))
        {
            return NotFound();
        }

        IssuedSetupToken = "hp_setup_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return RedirectToPage(new { id });
    }

    private async Task LoadInstallationsAsync(Guid licenseId, CancellationToken cancellationToken)
    {
        var installations = await dbContext.Installations
            .AsNoTracking()
            .Where(x => x.LicenseId == licenseId)
            .Select(x => new InstallationRow(
                x.Id,
                x.InstallationId,
                x.HostName,
                x.ApplicationVersion,
                x.LastSeenUtc,
                x.RevokedUtc))
            .ToListAsync(cancellationToken);
        Installations = installations
            .OrderByDescending(x => x.LastSeenUtc)
            .ToList();
    }

    private void BuildDeploymentTemplate(CustomerLicense license, string setupToken)
    {
        var publicUrl = (configuration["Authority:PublicUrl"] ?? "https://YOUR-LICENSE-AUTHORITY").TrimEnd('/');
        DeploymentTemplate = $"""
ConnectionStrings__DefaultConnection=<CUSTOMER_POSTGRES_CONNECTION>
Licensing__RequireLicense=true
Licensing__ValidationUrl={publicUrl}/api/v1/licenses/validate
Licensing__ManagementUrl={publicUrl}/Admin
Licensing__LicenseKey=<CUSTOMER_LICENSE_KEY>
Licensing__ProductCode={license.ProductCode}
Licensing__InstallationId=<CUSTOMER_STABLE_INSTALLATION_GUID>
Licensing__ValidationIntervalMinutes=1
Licensing__OfflineGraceMinutes=1
Provisioning__SetupToken={(string.IsNullOrWhiteSpace(setupToken) ? "<CUSTOMER_SETUP_TOKEN>" : setupToken)}
PaymentGateway__WebhookSecret=<CUSTOMER_WEBHOOK_SECRET>
Resend__ApiToken=<CUSTOMER_EMAIL_API_TOKEN>
""";
    }

    private void BuildApplicationLinks(string applicationUrl, string setupToken)
    {
        if (!Uri.TryCreate(applicationUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            PublicApplicationUrl = string.Empty;
            OwnerSetupUrl = string.Empty;
            AdminLoginUrl = string.Empty;
            TenantApplicationUrl = string.Empty;
            return;
        }

        var pathBase = uri.AbsolutePath.TrimEnd('/');
        PublicApplicationUrl = $"{uri.GetLeftPart(UriPartial.Authority).TrimEnd('/')}{pathBase}";
        OwnerSetupUrl = string.IsNullOrWhiteSpace(setupToken)
            ? string.Empty
            : $"{PublicApplicationUrl}/Setup?token={Uri.EscapeDataString(setupToken)}";
        AdminLoginUrl = $"{PublicApplicationUrl}/Login?returnUrl=%2Fadmin%2FDashboard";
        TenantApplicationUrl = $"{PublicApplicationUrl}/apply";
    }

    public sealed class LicenseInput
    {
        [Required, MaxLength(200)]
        [Display(Name = "Customer name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required, MaxLength(80), RegularExpression("^[a-z0-9][a-z0-9._-]*$")]
        [Display(Name = "Product code")]
        public string ProductCode { get; set; } = string.Empty;

        public LicenseStatus Status { get; set; }

        [Required, DataType(DataType.DateTime)]
        [Display(Name = "Paid through (UTC)")]
        public DateTime PaidThroughUtc { get; set; }

        [Range(1, 1000)]
        [Display(Name = "Maximum installations")]
        public int MaxInstallations { get; set; }

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;

    }

    public sealed class DeploymentInput
    {
        [MaxLength(500)]
        [Display(Name = "Application base URL")]
        public string ApplicationUrl { get; set; } = string.Empty;

        [Required, MaxLength(60)]
        [Display(Name = "Hosting provider")]
        public string HostingProvider { get; set; } = "manual";

        [MaxLength(80)]
        [Display(Name = "Deployment region")]
        public string DeploymentRegion { get; set; } = string.Empty;

        [Required, MaxLength(30)]
        [RegularExpression("^(not_configured|ready|deploying|deployed|failed)$")]
        [Display(Name = "Deployment status")]
        public string DeploymentStatus { get; set; } = "not_configured";

        [MaxLength(200)]
        [Display(Name = "External secret reference")]
        public string SecretReference { get; set; } = string.Empty;

        public static DeploymentInput FromLicense(CustomerLicense license) => new()
        {
            ApplicationUrl = license.ApplicationUrl,
            HostingProvider = license.HostingProvider,
            DeploymentRegion = license.DeploymentRegion,
            DeploymentStatus = license.DeploymentStatus,
            SecretReference = license.SecretReference
        };
    }

    public sealed record InstallationRow(
        Guid Id,
        string InstallationId,
        string HostName,
        string ApplicationVersion,
        DateTimeOffset LastSeenUtc,
        DateTimeOffset? RevokedUtc);
}
