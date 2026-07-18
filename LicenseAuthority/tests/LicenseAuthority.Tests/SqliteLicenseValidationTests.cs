using HostelPro.LicenseAuthority.Contracts;
using HostelPro.LicenseAuthority.Data;
using HostelPro.LicenseAuthority.Domain;
using HostelPro.LicenseAuthority.Options;
using HostelPro.LicenseAuthority.Security;
using HostelPro.LicenseAuthority.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HostelPro.LicenseAuthority.Tests;

public sealed class SqliteLicenseValidationTests
{
    [Fact]
    public async Task Development_database_can_register_and_validate_an_installation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<LicenseAuthorityDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var dbContext = new LicenseAuthorityDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();

        var keyService = new LicenseKeyService();
        var issued = keyService.Issue();
        var now = DateTimeOffset.UtcNow;
        dbContext.Licenses.Add(new CustomerLicense
        {
            CustomerName = "Test Hostel",
            ProductCode = "hostelpro",
            KeyPrefix = issued.Prefix,
            KeyHash = issued.Hash,
            Status = LicenseStatus.Active,
            PaidThroughUtc = now.AddDays(7),
            MaxInstallations = 1,
            CreatedUtc = now,
            UpdatedUtc = now
        });
        await dbContext.SaveChangesAsync();

        var service = new LicenseValidationService(
            dbContext,
            keyService,
            Microsoft.Extensions.Options.Options.Create(
                new LicenseValidationOptions { LeaseMinutes = 1 }),
            TimeProvider.System);
        var result = await service.ValidateAsync(
            issued.Plaintext,
            new LicenseValidationRequest
            {
                ProductCode = "hostelpro",
                InstallationId = Guid.NewGuid().ToString("D"),
                ApplicationVersion = "1.0.0",
                HostName = "test-host"
            },
            CancellationToken.None);

        Assert.True(result.KeyRecognized);
        Assert.True(result.Response?.Active);
        Assert.Equal("Test Hostel", result.Response?.CustomerName);
        Assert.Equal(1, await dbContext.Installations.CountAsync());
    }
}
