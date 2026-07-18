using HostelPro.LicenseAuthority.Domain;
using Microsoft.EntityFrameworkCore;

namespace HostelPro.LicenseAuthority.Data;

public sealed class LicenseAuthorityDbContext(DbContextOptions<LicenseAuthorityDbContext> options)
    : DbContext(options)
{
    public DbSet<CustomerLicense> Licenses => Set<CustomerLicense>();
    public DbSet<LicenseInstallation> Installations => Set<LicenseInstallation>();
    public DbSet<VendorAdmin> VendorAdmins => Set<VendorAdmin>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var licenses = modelBuilder.Entity<CustomerLicense>();
        licenses.ToTable("Licenses", table =>
            table.HasCheckConstraint("CK_Licenses_MaxInstallations", "\"MaxInstallations\" > 0"));
        licenses.HasKey(x => x.Id);
        licenses.Property(x => x.CustomerName).IsRequired();
        licenses.Property(x => x.ProductCode).IsRequired();
        licenses.Property(x => x.KeyPrefix).IsRequired();
        licenses.Property(x => x.KeyHash).HasColumnType("character(64)").IsRequired();
        licenses.Property(x => x.Status)
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => Enum.Parse<LicenseStatus>(value, true))
            .HasMaxLength(20)
            .IsRequired();
        licenses.Property(x => x.Notes).HasDefaultValue(string.Empty);
        licenses.Property(x => x.ApplicationUrl).HasDefaultValue(string.Empty);
        licenses.Property(x => x.HostingProvider).HasDefaultValue("manual");
        licenses.Property(x => x.DeploymentRegion).HasDefaultValue(string.Empty);
        licenses.Property(x => x.DeploymentStatus).HasDefaultValue("not_configured");
        licenses.Property(x => x.SecretReference).HasDefaultValue(string.Empty);
        licenses.HasIndex(x => x.KeyHash).IsUnique();
        licenses.HasIndex(x => x.KeyPrefix).IsUnique();
        licenses.HasIndex(x => new { x.ProductCode, x.Status });

        var installations = modelBuilder.Entity<LicenseInstallation>();
        installations.ToTable("LicenseInstallations");
        installations.HasKey(x => x.Id);
        installations.Property(x => x.InstallationId).IsRequired();
        installations.Property(x => x.HostName).IsRequired();
        installations.Property(x => x.ApplicationVersion).IsRequired();
        installations.HasIndex(x => new { x.LicenseId, x.InstallationId }).IsUnique();
        installations.HasIndex(x => new { x.LicenseId, x.RevokedUtc });
        installations.HasOne(x => x.License)
            .WithMany(x => x.Installations)
            .HasForeignKey(x => x.LicenseId)
            .OnDelete(DeleteBehavior.Cascade);

        var admins = modelBuilder.Entity<VendorAdmin>();
        admins.ToTable("VendorAdmins");
        admins.HasKey(x => x.Id);
        admins.Property(x => x.Email).IsRequired();
        admins.Property(x => x.NormalizedEmail).IsRequired();
        admins.Property(x => x.PasswordHash).IsRequired();
        admins.HasIndex(x => x.NormalizedEmail).IsUnique();
    }
}
