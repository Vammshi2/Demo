namespace HostelPro.LicenseAuthority.Options;

public sealed class LicenseValidationOptions
{
    public const string SectionName = "LicenseValidation";
    public int LeaseMinutes { get; set; } = 1;
}
