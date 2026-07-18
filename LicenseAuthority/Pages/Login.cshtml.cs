using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using HostelPro.LicenseAuthority.Data;
using HostelPro.LicenseAuthority.Domain;
using HostelPro.LicenseAuthority.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace HostelPro.LicenseAuthority.Pages;

[AllowAnonymous]
[EnableRateLimiting("auth")]
public sealed class LoginModel(
    LicenseAuthorityDbContext dbContext,
    IPasswordHasher<VendorAdmin> passwordHasher,
    TimeProvider timeProvider) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.VendorAdmins.AnyAsync(cancellationToken))
        {
            return RedirectToPage("/Setup");
        }

        return User.Identity?.IsAuthenticated == true
            ? RedirectToPage("/Admin/Index")
            : Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var normalizedEmail = Input.Email.Trim().ToUpperInvariant();
        var admin = await dbContext.VendorAdmins.SingleOrDefaultAsync(
            x => x.NormalizedEmail == normalizedEmail,
            cancellationToken);
        var verification = admin is null
            ? PasswordVerificationResult.Failed
            : passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, Input.Password);
        if (admin is null || verification == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "The email or password is invalid.");
            return Page();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            admin.PasswordHash = passwordHasher.HashPassword(admin, Input.Password);
        }

        admin.LastLoginUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Name, admin.Email)
        ], VendorAuthenticationDefaults.Scheme));
        await HttpContext.SignInAsync(
            VendorAuthenticationDefaults.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = Input.RememberMe,
                AllowRefresh = true
            });

        return !string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
            ? LocalRedirect(ReturnUrl)
            : RedirectToPage("/Admin/Index");
    }

    public sealed class LoginInput
    {
        [Required, EmailAddress, MaxLength(254)]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}
