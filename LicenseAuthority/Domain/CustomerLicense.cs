using System.ComponentModel.DataAnnotations;

namespace HostelPro.LicenseAuthority.Domain;

public sealed class CustomerLicense
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ProductCode { get; set; } = "hostelpro";

    [MaxLength(24)]
    public string KeyPrefix { get; set; } = string.Empty;

    [MaxLength(64)]
    public string KeyHash { get; set; } = string.Empty;

    public LicenseStatus Status { get; set; } = LicenseStatus.Trial;
    public DateTimeOffset PaidThroughUtc { get; set; }
    public int MaxInstallations { get; set; } = 1;

    [MaxLength(1000)]
    public string Notes { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ApplicationUrl { get; set; } = string.Empty;

    [MaxLength(60)]
    public string HostingProvider { get; set; } = "manual";

    [MaxLength(80)]
    public string DeploymentRegion { get; set; } = string.Empty;

    [MaxLength(30)]
    public string DeploymentStatus { get; set; } = "not_configured";

    [MaxLength(200)]
    public string SecretReference { get; set; } = string.Empty;

    public DateTimeOffset? LastDeployedUtc { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public ICollection<LicenseInstallation> Installations { get; set; } = new List<LicenseInstallation>();
}
