using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HostelPro.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace HostelPro.Services;

public interface ILicenseService
{
    Task<LicenseSnapshot> GetStatusAsync(bool forceRefresh = false, CancellationToken cancellationToken = default);
}

public sealed class LicenseService : ILicenseService
{
    private const string CachePurpose = "HostelPro.Licensing.Cache.v1";
    private readonly IHttpClientFactory httpClientFactory;
    private readonly LicensingOptions options;
    private readonly IWebHostEnvironment environment;
    private readonly IDataProtector cacheProtector;
    private readonly ILogger<LicenseService> logger;
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly string cachePath;
    private readonly string installationIdPath;
    private LicenseSnapshot? current;

    public LicenseService(
        IHttpClientFactory httpClientFactory,
        IOptions<LicensingOptions> options,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<LicenseService> logger)
    {
        this.httpClientFactory = httpClientFactory;
        this.options = options.Value;
        this.environment = environment;
        this.logger = logger;
        cacheProtector = dataProtectionProvider.CreateProtector(CachePurpose);

        var configuredDataDirectory = configuration["Storage:DataPath"];
        var dataDirectory = string.IsNullOrWhiteSpace(configuredDataDirectory)
            ? Path.Combine(environment.ContentRootPath, "App_Data")
            : configuredDataDirectory;
        Directory.CreateDirectory(dataDirectory);
        cachePath = Path.Combine(dataDirectory, "license-cache.dat");
        installationIdPath = Path.Combine(dataDirectory, "installation-id.txt");
    }

    public async Task<LicenseSnapshot> GetStatusAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!options.RequireLicense)
        {
            return CreateLocalAccess("disabled", "License enforcement is disabled for this deployment.");
        }

        if (environment.IsDevelopment() && !HasRemoteConfiguration())
        {
            return CreateLocalAccess("development", "Development mode license bypass.");
        }

        if (!HasRemoteConfiguration())
        {
            return CreateBlocked(
                "configuration_required",
                "This installation has not been activated. Configure the vendor license URL and installation key.");
        }

        current ??= await ReadCacheAsync(cancellationToken);
        var interval = TimeSpan.FromMinutes(Math.Clamp(options.ValidationIntervalMinutes, 1, 1440));
        if (!forceRefresh && current is not null && DateTimeOffset.UtcNow < current.CheckedAtUtc.Add(interval))
        {
            return current with { NextCheckUtc = current.CheckedAtUtc.Add(interval) };
        }

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!forceRefresh && current is not null && DateTimeOffset.UtcNow < current.CheckedAtUtc.Add(interval))
            {
                return current with { NextCheckUtc = current.CheckedAtUtc.Add(interval) };
            }

            current = await ValidateRemotelyAsync(interval, cancellationToken);
            return current;
        }
        finally
        {
            refreshLock.Release();
        }
    }

    private bool HasRemoteConfiguration()
    {
        return Uri.TryCreate(options.ValidationUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                || (environment.IsDevelopment()
                    && uri.IsLoopback
                    && uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)))
            && !string.IsNullOrWhiteSpace(options.LicenseKey)
            && !options.LicenseKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<LicenseSnapshot> ValidateRemotelyAsync(
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, options.ValidationUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.LicenseKey.Trim());
            request.Content = JsonContent.Create(new LicenseValidationRequest(
                options.ProductCode.Trim(),
                await GetInstallationIdAsync(cancellationToken),
                Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                Environment.MachineName));

            var client = httpClientFactory.CreateClient("HostelProLicense");
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode is 401 or 402 or 403 or 404)
                {
                    var rejected = CreateBlocked(
                        "suspended",
                        "This installation is inactive or its subscription payment is overdue.");
                    await WriteCacheAsync(rejected, cancellationToken);
                    return rejected;
                }

                throw new HttpRequestException($"License server returned HTTP {(int)response.StatusCode}.");
            }

            var result = await response.Content.ReadFromJsonAsync<LicenseValidationResponse>(cancellationToken)
                ?? throw new InvalidOperationException("License server returned an empty response.");
            var now = DateTimeOffset.UtcNow;
            var leaseExpiry = result.LeaseExpiresUtc ?? now.Add(interval);
            var active = result.Active
                && leaseExpiry > now
                && !result.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase)
                && !result.Status.Equals("unpaid", StringComparison.OrdinalIgnoreCase)
                && !result.Status.Equals("expired", StringComparison.OrdinalIgnoreCase);

            var snapshot = new LicenseSnapshot(
                active ? NormalizeActiveState(result.Status) : "suspended",
                active,
                result.CustomerName.Trim(),
                active
                    ? ValueOrDefault(result.Message, "Subscription verified.")
                    : ValueOrDefault(result.Message, "Subscription payment is overdue. Contact the software provider."),
                now,
                result.PaidThroughUtc,
                leaseExpiry,
                now.Add(interval));

            await WriteCacheAsync(snapshot, cancellationToken);
            return snapshot;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Unable to validate the application license with the configured vendor service.");
            return BuildOfflineStatus(interval);
        }
    }

    private LicenseSnapshot BuildOfflineStatus(TimeSpan interval)
    {
        var now = DateTimeOffset.UtcNow;
        var grace = TimeSpan.FromMinutes(Math.Clamp(options.OfflineGraceMinutes, 1, 10080));
        var graceExpiry = current?.CheckedAtUtc.Add(grace);
        var usableUntil = Earliest(current?.LeaseExpiresUtc, graceExpiry);

        if (current?.AllowsAccess == true && usableUntil > now)
        {
            return current with
            {
                State = "grace",
                AllowsAccess = true,
                Message = "The license service is temporarily unavailable. This installation is running in its offline grace period.",
                NextCheckUtc = now.Add(interval)
            };
        }

        return new LicenseSnapshot(
            "validation_unavailable",
            false,
            current?.CustomerName ?? string.Empty,
            "The subscription could not be verified and the offline grace period has ended. Contact the software provider.",
            now,
            current?.PaidThroughUtc,
            current?.LeaseExpiresUtc,
            now.Add(interval));
    }

    private async Task<string> GetInstallationIdAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(options.InstallationId)
            && Guid.TryParse(options.InstallationId.Trim(), out _))
        {
            return options.InstallationId.Trim();
        }

        if (File.Exists(installationIdPath))
        {
            var existing = (await File.ReadAllTextAsync(installationIdPath, cancellationToken)).Trim();
            if (Guid.TryParse(existing, out _))
            {
                return existing;
            }
        }

        var installationId = Guid.NewGuid().ToString("D");
        await File.WriteAllTextAsync(installationIdPath, installationId, cancellationToken);
        return installationId;
    }

    private async Task<LicenseSnapshot?> ReadCacheAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(cachePath))
        {
            return null;
        }

        try
        {
            var protectedJson = await File.ReadAllTextAsync(cachePath, cancellationToken);
            var json = cacheProtector.Unprotect(protectedJson);
            var cached = JsonSerializer.Deserialize<LicenseCacheRecord>(json);
            return cached is null || !cached.Binding.Equals(CreateCacheBinding(), StringComparison.Ordinal)
                ? null
                : new LicenseSnapshot(
                    cached.State,
                    cached.AllowsAccess,
                    cached.CustomerName,
                    cached.Message,
                    cached.CheckedAtUtc,
                    cached.PaidThroughUtc,
                    cached.LeaseExpiresUtc,
                    null);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Ignoring an invalid local license cache.");
            return null;
        }
    }

    private async Task WriteCacheAsync(LicenseSnapshot snapshot, CancellationToken cancellationToken)
    {
        var record = new LicenseCacheRecord
        {
            Binding = CreateCacheBinding(),
            State = snapshot.State,
            AllowsAccess = snapshot.AllowsAccess,
            CustomerName = snapshot.CustomerName,
            Message = snapshot.Message,
            CheckedAtUtc = snapshot.CheckedAtUtc,
            PaidThroughUtc = snapshot.PaidThroughUtc,
            LeaseExpiresUtc = snapshot.LeaseExpiresUtc
        };
        var protectedJson = cacheProtector.Protect(JsonSerializer.Serialize(record));
        await File.WriteAllTextAsync(cachePath, protectedJson, cancellationToken);
    }

    private string CreateCacheBinding()
    {
        var material = $"{options.ProductCode.Trim().ToLowerInvariant()}\n{options.LicenseKey.Trim()}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private LicenseSnapshot CreateLocalAccess(string state, string message)
    {
        var now = DateTimeOffset.UtcNow;
        return new LicenseSnapshot(state, true, string.Empty, message, now, null, null, now.AddHours(12));
    }

    private LicenseSnapshot CreateBlocked(string state, string message)
    {
        var now = DateTimeOffset.UtcNow;
        return new LicenseSnapshot(state, false, string.Empty, message, now, null, null, now.AddMinutes(15));
    }

    private static string NormalizeActiveState(string state)
    {
        return state.Equals("trial", StringComparison.OrdinalIgnoreCase) ? "trial" : "active";
    }

    private static string ValueOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static DateTimeOffset? Earliest(DateTimeOffset? first, DateTimeOffset? second)
    {
        if (!first.HasValue)
        {
            return second;
        }

        if (!second.HasValue)
        {
            return first;
        }

        return first < second ? first : second;
    }
}
