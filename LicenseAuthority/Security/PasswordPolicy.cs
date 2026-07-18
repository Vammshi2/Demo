namespace HostelPro.LicenseAuthority.Security;

public static class PasswordPolicy
{
    public static string? Validate(string password)
    {
        if (password.Length < 12)
        {
            return "Use at least 12 characters.";
        }

        if (!password.Any(char.IsUpper) || !password.Any(char.IsLower) || !password.Any(char.IsDigit))
        {
            return "Include uppercase, lowercase, and numeric characters.";
        }

        return null;
    }
}
