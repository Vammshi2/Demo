using HostelPro.LicenseAuthority.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HostelPro.LicenseAuthority.Pages;

[Authorize]
public sealed class LogoutModel : PageModel
{
    public IActionResult OnGet() => RedirectToPage("/Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync(VendorAuthenticationDefaults.Scheme);
        return RedirectToPage("/Login");
    }
}
