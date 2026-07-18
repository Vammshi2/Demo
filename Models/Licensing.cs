using System.ComponentModel.DataAnnotations;

namespace HostelPro.Models;

public sealed class LicensingOptions
{
    public const string SectionName = "Licensing";

    public bool RequireLicense { get; set; } = true;

    [MaxLength(500)]
    public string ValidationUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string ManagementUrl { get; set; } = string.Empty;

    [MaxLength(300)]
    public string LicenseKey { get; set; } = string.Empty;

    [MaxLength(80)]
    public string ProductCode { get; set; } = "hostelpro";

    [MaxLength(80)]
    public string InstallationId { get; set; } = string.Empty;

    public int ValidationIntervalMinutes { get; set; } = 1;
    public int OfflineGraceMinutes { get; set; } = 1;
}

public sealed class ProvisioningOptions
{
    public const string SectionName = "Provisioning";

    [MaxLength(300)]
    public string SetupToken { get; set; } = string.Empty;
}

public sealed record LicenseSnapshot(
    string State,
    bool AllowsAccess,
    string CustomerName,
    string Message,
    DateTimeOffset CheckedAtUtc,
    DateTimeOffset? PaidThroughUtc,
    DateTimeOffset? LeaseExpiresUtc,
    DateTimeOffset? NextCheckUtc)
{
    public bool IsGracePeriod => State.Equals("grace", StringComparison.OrdinalIgnoreCase);
}

public sealed record LicenseValidationRequest(
    string ProductCode,
    string InstallationId,
    string ApplicationVersion,
    string HostName);

public sealed class LicenseValidationResponse
{
    public bool Active { get; set; }
    public string Status { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset? PaidThroughUtc { get; set; }
    public DateTimeOffset? LeaseExpiresUtc { get; set; }
}

internal sealed class LicenseCacheRecord
{
    public string Binding { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool AllowsAccess { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CheckedAtUtc { get; set; }
    public DateTimeOffset? PaidThroughUtc { get; set; }
    public DateTimeOffset? LeaseExpiresUtc { get; set; }
}
