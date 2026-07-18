namespace HostelPro.LicenseAuthority.Options;

public sealed class AdminSetupOptions
{
    public const string SectionName = "AdminSetup";
    public string BootstrapToken { get; set; } = string.Empty;
}
