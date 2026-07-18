using System.Data;
using HostelPro.LicenseAuthority.Contracts;
using HostelPro.LicenseAuthority.Data;
using HostelPro.LicenseAuthority.Domain;
using HostelPro.LicenseAuthority.Options;
using HostelPro.LicenseAuthority.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HostelPro.LicenseAuthority.Services;

public sealed record LicenseValidationResult(bool KeyRecognized, LicenseValidationResponse? Response)
{
    public static LicenseValidationResult UnknownKey() => new(false, null);
    public static LicenseValidationResult Recognized(LicenseValidationResponse response) => new(true, response);
}

public interface ILicenseValidationService
{
    Task<LicenseValidationResult> ValidateAsync(
        string plaintextKey,
        LicenseValidationRequest request,
        CancellationToken cancellationToken);
}

public sealed class LicenseValidationService(
    LicenseAuthorityDbContext dbContext,
    ILicenseKeyService keyService,
    IOptions<LicenseValidationOptions> options,
    TimeProvider timeProvider) : ILicenseValidationService
{
    public async Task<LicenseValidationResult> ValidateAsync(
        string plaintextKey,
        LicenseValidationRequest request,
        CancellationToken cancellationToken)
    {
        if (!keyService.TryHash(plaintextKey, out var keyHash))
        {
            return LicenseValidationResult.UnknownKey();
        }

        var now = timeProvider.GetUtcNow();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        // PostgreSQL row locking prevents concurrent first validations from exceeding the limit.
        // SQLite serializes writes at the database level and does not support FOR UPDATE.
        var licenseQuery = dbContext.Database.IsNpgsql()
            ? dbContext.Licenses.FromSqlInterpolated(
                $"SELECT * FROM \"Licenses\" WHERE \"KeyHash\" = {keyHash} FOR UPDATE")
            : dbContext.Licenses.Where(x => x.KeyHash == keyHash);
        var license = await licenseQuery.SingleOrDefaultAsync(cancellationToken);
        if (license is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return LicenseValidationResult.UnknownKey();
        }

        if (!license.ProductCode.Equals(request.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            return LicenseValidationResult.Recognized(Blocked(
                license,
                "suspended",
                "This key is not valid for the requested product."));
        }

        var installationId = request.InstallationId.Trim();
        var installation = await dbContext.Installations.SingleOrDefaultAsync(
            x => x.LicenseId == license.Id && x.InstallationId == installationId,
            cancellationToken);
        if (installation?.RevokedUtc is not null)
        {
            installation.LastSeenUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return LicenseValidationResult.Recognized(Blocked(
                license,
                "suspended",
                "This installation has been revoked by the software provider."));
        }

        var access = LicenseDecisionEvaluator.Evaluate(license.Status, license.PaidThroughUtc, now);
        if (!access.Active)
        {
            if (installation is not null)
            {
                installation.LastSeenUtc = now;
                installation.HostName = request.HostName.Trim();
                installation.ApplicationVersion = request.ApplicationVersion.Trim();
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return LicenseValidationResult.Recognized(Blocked(license, access.Status, access.Message));
        }

        if (installation is null)
        {
            var activeInstallations = await dbContext.Installations.CountAsync(
                x => x.LicenseId == license.Id && x.RevokedUtc == null,
                cancellationToken);
            if (activeInstallations >= license.MaxInstallations)
            {
                await transaction.CommitAsync(cancellationToken);
                return LicenseValidationResult.Recognized(Blocked(
                    license,
                    "suspended",
                    "The maximum number of installations has been reached."));
            }

            installation = new LicenseInstallation
            {
                LicenseId = license.Id,
                InstallationId = installationId,
                HostName = request.HostName.Trim(),
                ApplicationVersion = request.ApplicationVersion.Trim(),
                FirstSeenUtc = now,
                LastSeenUtc = now,
                LastValidatedUtc = now
            };
            dbContext.Installations.Add(installation);
        }
        else
        {
            installation.HostName = request.HostName.Trim();
            installation.ApplicationVersion = request.ApplicationVersion.Trim();
            installation.LastSeenUtc = now;
            installation.LastValidatedUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var leaseMinutes = Math.Clamp(options.Value.LeaseMinutes, 1, 1440);
        var leaseExpires = now.AddMinutes(leaseMinutes);
        if (license.PaidThroughUtc < leaseExpires)
        {
            leaseExpires = license.PaidThroughUtc;
        }

        return LicenseValidationResult.Recognized(new LicenseValidationResponse
        {
            Active = true,
            Status = access.Status,
            CustomerName = license.CustomerName,
            Message = access.Message,
            PaidThroughUtc = license.PaidThroughUtc,
            LeaseExpiresUtc = leaseExpires
        });
    }

    private static LicenseValidationResponse Blocked(
        CustomerLicense license,
        string status,
        string message) => new()
        {
            Active = false,
            Status = status,
            CustomerName = license.CustomerName,
            Message = message,
            PaidThroughUtc = license.PaidThroughUtc,
            LeaseExpiresUtc = null
        };
}
