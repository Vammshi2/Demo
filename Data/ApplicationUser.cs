using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace HostelPro.Data;

public sealed class ApplicationUser : IdentityUser
{
    [MaxLength(160)]
    public string FullName { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginUtc { get; set; }
}
