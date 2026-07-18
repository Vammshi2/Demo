using System.ComponentModel.DataAnnotations;
using HostelPro.LicenseAuthority.Data;
using HostelPro.LicenseAuthority.Domain;
using HostelPro.LicenseAuthority.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Cryptography;

namespace HostelPro.LicenseAuthority.Pages.Admin;

[Authorize]
public sealed class CreateModel(
    LicenseAuthorityDbContext dbContext,
    ILicenseKeyService keyService,
    TimeProvider timeProvider) : PageModel
{
    [BindProperty]
    public LicenseInput Input { get; set; } = new();

    public string IssuedKey { get; private set; } = string.Empty;
    public string IssuedSetupToken { get; private set; } = string.Empty;

    public void OnGet()
    {
        Input.PaidThroughUtc = DateTime.UtcNow.AddDays(14);
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var issued = keyService.Issue();
        var now = timeProvider.GetUtcNow();
        var license = new CustomerLicense
        {
            CustomerName = Input.CustomerName.Trim(),
            ProductCode = Input.ProductCode.Trim().ToLowerInvariant(),
            KeyPrefix = issued.Prefix,
            KeyHash = issued.Hash,
            Status = Input.Status,
            PaidThroughUtc = AsUtc(Input.PaidThroughUtc),
            MaxInstallations = Input.MaxInstallations,
            Notes = Input.Notes.Trim(),
            CreatedUtc = now,
            UpdatedUtc = now
        };
        dbContext.Licenses.Add(license);
        await dbContext.SaveChangesAsync(cancellationToken);

        IssuedKey = issued.Plaintext;
        IssuedSetupToken = IssueSetupToken();
        return Page();
    }

    private static string IssueSetupToken() =>
        "hp_setup_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static DateTimeOffset AsUtc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    public sealed class LicenseInput
    {
        [Required, MaxLength(200)]
        [Display(Name = "Customer name")]
        public string CustomerName { get; set; } = string.Empty;

        [Required, MaxLength(80), RegularExpression("^[a-z0-9][a-z0-9._-]*$")]
        [Display(Name = "Product code")]
        public string ProductCode { get; set; } = "hostelpro";

        public LicenseStatus Status { get; set; } = LicenseStatus.Trial;

        [Required, DataType(DataType.DateTime)]
        [Display(Name = "Paid through (UTC)")]
        public DateTime PaidThroughUtc { get; set; }

        [Range(1, 1000)]
        [Display(Name = "Maximum installations")]
        public int MaxInstallations { get; set; } = 1;

        [MaxLength(1000)]
        public string Notes { get; set; } = string.Empty;
    }
}
