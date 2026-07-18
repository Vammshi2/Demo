using System.Security.Cryptography;
using System.Text;
using HostelPro.Models;
using Microsoft.Extensions.Options;

namespace HostelPro.Services;

public interface ISetupTokenValidator
{
    bool IsConfigured { get; }
    bool Validate(string? candidate);
}

public sealed class SetupTokenValidator : ISetupTokenValidator
{
    private readonly byte[]? expectedHash;

    public SetupTokenValidator(IOptions<ProvisioningOptions> options)
    {
        var token = (options.Value.SetupToken ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(token))
        {
            expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        }
    }

    public bool IsConfigured => expectedHash is not null;

    public bool Validate(string? candidate)
    {
        if (expectedHash is null || string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate.Trim()));
        return CryptographicOperations.FixedTimeEquals(expectedHash, candidateHash);
    }
}
