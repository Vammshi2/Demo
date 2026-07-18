using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace HostelPro.LicenseAuthority.Security;

public sealed record IssuedLicenseKey(string Plaintext, string Prefix, string Hash);

public interface ILicenseKeyService
{
    IssuedLicenseKey Issue();
    bool TryHash(string plaintext, out string hash);
}

public sealed class LicenseKeyService : ILicenseKeyService
{
    private const string Marker = "hp_live_";
    private const int PrefixBodyLength = 12;
    private const int RandomByteCount = 32;

    public IssuedLicenseKey Issue()
    {
        var body = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(RandomByteCount));
        var plaintext = Marker + body;
        var prefix = Marker + body[..PrefixBodyLength];
        return new IssuedLicenseKey(plaintext, prefix, Hash(plaintext));
    }

    public bool TryHash(string plaintext, out string hash)
    {
        var value = plaintext.Trim();
        if (!value.StartsWith(Marker, StringComparison.Ordinal) || value.Length is < 40 or > 100)
        {
            hash = string.Empty;
            return false;
        }

        hash = Hash(value);
        return true;
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
