using HostelPro.LicenseAuthority.Security;

namespace HostelPro.LicenseAuthority.Tests;

public sealed class LicenseKeyServiceTests
{
    [Fact]
    public void Issued_key_can_be_resolved_to_its_stored_hash()
    {
        var service = new LicenseKeyService();
        var issued = service.Issue();

        Assert.StartsWith("hp_live_", issued.Plaintext);
        Assert.StartsWith("hp_live_", issued.Prefix);
        Assert.DoesNotContain(issued.Plaintext, issued.Hash, StringComparison.Ordinal);
        Assert.True(service.TryHash(issued.Plaintext, out var recalculated));
        Assert.Equal(issued.Hash, recalculated);
        Assert.Equal(64, issued.Hash.Length);
    }

    [Fact]
    public void Malformed_key_is_rejected_before_database_lookup()
    {
        var service = new LicenseKeyService();

        Assert.False(service.TryHash("not-a-license-key", out _));
    }
}
