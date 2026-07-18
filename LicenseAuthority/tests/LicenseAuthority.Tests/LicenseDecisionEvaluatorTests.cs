using HostelPro.LicenseAuthority.Domain;
using HostelPro.LicenseAuthority.Services;

namespace HostelPro.LicenseAuthority.Tests;

public sealed class LicenseDecisionEvaluatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 18, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Active_license_before_paid_through_date_allows_validation()
    {
        var result = LicenseDecisionEvaluator.Evaluate(
            LicenseStatus.Active,
            Now.AddDays(10),
            Now);

        Assert.True(result.Active);
        Assert.Equal("active", result.Status);
    }

    [Fact]
    public void Trial_license_before_paid_through_date_remains_trial()
    {
        var result = LicenseDecisionEvaluator.Evaluate(
            LicenseStatus.Trial,
            Now.AddDays(2),
            Now);

        Assert.True(result.Active);
        Assert.Equal("trial", result.Status);
    }

    [Fact]
    public void Suspended_license_is_blocked_even_when_paid_through()
    {
        var result = LicenseDecisionEvaluator.Evaluate(
            LicenseStatus.Suspended,
            Now.AddMonths(1),
            Now);

        Assert.False(result.Active);
        Assert.Equal("suspended", result.Status);
    }

    [Fact]
    public void Unpaid_license_is_blocked()
    {
        var result = LicenseDecisionEvaluator.Evaluate(
            LicenseStatus.Unpaid,
            Now.AddMonths(1),
            Now);

        Assert.False(result.Active);
        Assert.Equal("unpaid", result.Status);
    }

    [Fact]
    public void Paid_through_at_validation_time_is_expired()
    {
        var result = LicenseDecisionEvaluator.Evaluate(
            LicenseStatus.Active,
            Now,
            Now);

        Assert.False(result.Active);
        Assert.Equal("expired", result.Status);
    }
}
