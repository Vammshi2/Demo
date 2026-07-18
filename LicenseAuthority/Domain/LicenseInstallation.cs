using System.ComponentModel.DataAnnotations;

namespace HostelPro.LicenseAuthority.Domain;

public sealed class LicenseInstallation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LicenseId { get; set; }
    public CustomerLicense License { get; set; } = null!;

    [MaxLength(80)]
    public string InstallationId { get; set; } = string.Empty;

    [MaxLength(255)]
    public string HostName { get; set; } = string.Empty;

    [MaxLength(40)]
    public string ApplicationVersion { get; set; } = string.Empty;

    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
    public DateTimeOffset? LastValidatedUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
}
