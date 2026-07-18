using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HostelPro.LicenseAuthority.Data;

public sealed class LicenseAuthorityDbContextFactory : IDesignTimeDbContextFactory<LicenseAuthorityDbContext>
{
    public LicenseAuthorityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__LicenseAuthority")
            ?? throw new InvalidOperationException(
                "ConnectionStrings__LicenseAuthority is required for design-time migration commands.");
        var options = new DbContextOptionsBuilder<LicenseAuthorityDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new LicenseAuthorityDbContext(options);
    }
}
