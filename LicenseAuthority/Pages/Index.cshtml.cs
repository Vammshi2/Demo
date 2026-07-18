using HostelPro.LicenseAuthority.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HostelPro.LicenseAuthority.Pages;

public sealed class IndexModel(LicenseAuthorityDbContext dbContext) : PageModel
{
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await dbContext.VendorAdmins.AnyAsync(cancellationToken))
        {
            return RedirectToPage("/Setup");
        }

        return User.Identity?.IsAuthenticated == true
            ? RedirectToPage("/Admin/Index")
            : RedirectToPage("/Login");
    }
}
