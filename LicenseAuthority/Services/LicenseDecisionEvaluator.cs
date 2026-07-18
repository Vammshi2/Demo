using HostelPro.LicenseAuthority.Domain;

namespace HostelPro.LicenseAuthority.Services;

public sealed record LicenseAccessDecision(bool Active, string Status, string Message);

public static class LicenseDecisionEvaluator
{
    public static LicenseAccessDecision Evaluate(
        LicenseStatus status,
        DateTimeOffset paidThroughUtc,
        DateTimeOffset nowUtc)
    {
        if (status == LicenseStatus.Suspended)
        {
            return new(false, "suspended", "This license has been suspended by the software provider.");
        }

        if (status == LicenseStatus.Unpaid)
        {
            return new(false, "unpaid", "Subscription payment is overdue. Contact the software provider.");
        }

        if (paidThroughUtc <= nowUtc)
        {
            return new(false, "expired", "The paid-through date has passed. Renew the subscription to restore access.");
        }

        return status == LicenseStatus.Trial
            ? new(true, "trial", "Trial license verified.")
            : new(true, "active", "Subscription verified.");
    }
}
