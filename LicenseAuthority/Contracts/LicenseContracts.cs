using System.ComponentModel.DataAnnotations;

namespace HostelPro.LicenseAuthority.Contracts;

public sealed class LicenseValidationRequest
{
    [Required, MaxLength(80)]
    public string ProductCode { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string InstallationId { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string ApplicationVersion { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string HostName { get; set; } = string.Empty;
}

public sealed class LicenseValidationResponse
{
    public bool Active { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset? PaidThroughUtc { get; set; }
    public DateTimeOffset? LeaseExpiresUtc { get; set; }
}
