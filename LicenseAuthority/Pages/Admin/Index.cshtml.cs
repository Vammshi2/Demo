using HostelPro.LicenseAuthority.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HostelPro.LicenseAuthority.Pages.Admin;

[Authorize]
public sealed class IndexModel(LicenseAuthorityDbContext dbContext) : PageModel
{
    public IReadOnlyList<LicenseRow> Licenses { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Licenses = await dbContext.Licenses
            .AsNoTracking()
            .OrderBy(x => x.CustomerName)
            .Select(x => new LicenseRow(
                x.Id,
                x.CustomerName,
                x.ProductCode,
                x.KeyPrefix,
                x.Status.ToString().ToLower(),
                x.PaidThroughUtc,
                x.MaxInstallations,
                x.Installations.Count(i => i.RevokedUtc == null)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostSetStatusAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var license = await dbContext.Licenses.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (license is null) return NotFound();
        if (!Enum.TryParse<HostelPro.LicenseAuthority.Domain.LicenseStatus>(status, true, out var parsed))
        {
            return BadRequest();
        }

        license.Status = parsed;
        license.UpdatedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        TempData["StatusMessage"] = parsed == HostelPro.LicenseAuthority.Domain.LicenseStatus.Unpaid
            ? "Customer marked unpaid. Active installations will stop on their next one-minute validation."
            : $"Customer status changed to {parsed}.";
        return RedirectToPage();
    }

    public sealed record LicenseRow(
        Guid Id,
        string CustomerName,
        string ProductCode,
        string KeyPrefix,
        string Status,
        DateTimeOffset PaidThroughUtc,
        int MaxInstallations,
        int ActiveInstallations);
}
