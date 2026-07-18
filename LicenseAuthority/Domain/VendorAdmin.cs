using System.ComponentModel.DataAnnotations;

namespace HostelPro.LicenseAuthority.Domain;

public sealed class VendorAdmin
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(254)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(254)]
    public string NormalizedEmail { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? LastLoginUtc { get; set; }
}
