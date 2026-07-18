using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using HostelPro.LicenseAuthority.Data;
using HostelPro.LicenseAuthority.Domain;
using HostelPro.LicenseAuthority.Options;
using HostelPro.LicenseAuthority.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HostelPro.LicenseAuthority.Pages;

[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class SetupModel(
    LicenseAuthorityDbContext dbContext,
    IPasswordHasher<VendorAdmin> passwordHasher,
    IOptions<AdminSetupOptions> options,
    TimeProvider timeProvider) : PageModel
{
    [BindProperty]
    public SetupInput Input { get; set; } = new();

    public bool BootstrapConfigured => !string.IsNullOrWhiteSpace(options.Value.BootstrapToken);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await dbContext.VendorAdmins.AnyAsync(cancellationToken)
            ? RedirectToPage("/Login")
            : Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.VendorAdmins.AnyAsync(cancellationToken))
        {
            return RedirectToPage("/Login");
        }

        if (!BootstrapConfigured)
        {
            ModelState.AddModelError(string.Empty, "The server bootstrap token is not configured.");
        }
        else if (!TokensMatch(options.Value.BootstrapToken, Input.BootstrapToken))
        {
            ModelState.AddModelError(string.Empty, "The bootstrap token is invalid.");
        }

        var passwordError = PasswordPolicy.Validate(Input.Password);
        if (passwordError is not null)
        {
            ModelState.AddModelError("Input.Password", passwordError);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var now = timeProvider.GetUtcNow();
        var admin = new VendorAdmin
        {
            Email = Input.Email.Trim(),
            NormalizedEmail = Input.Email.Trim().ToUpperInvariant(),
            CreatedUtc = now
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, Input.Password);
        dbContext.VendorAdmins.Add(admin);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "The administrator could not be created. Setup may already be complete.");
            return Page();
        }

        return RedirectToPage("/Login");
    }

    private static bool TokensMatch(string expected, string supplied)
    {
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        var suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(expectedHash, suppliedHash);
    }

    public sealed class SetupInput
    {
        [Required, EmailAddress, MaxLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(Password))]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        [Display(Name = "Bootstrap token")]
        public string BootstrapToken { get; set; } = string.Empty;
    }
}
