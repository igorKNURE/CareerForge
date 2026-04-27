namespace CareerForge.Infrastructure.Auth;

/// <summary>
/// Registration policy. When either <see cref="AllowedDomains"/> or
/// <see cref="AllowedEmails"/> is non-empty, the signup endpoint accepts only addresses
/// matched by the allow-list. When both are empty, registration is open to any email.
/// Comparison is case-insensitive.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>Domains permitted for registration (e.g. <c>nure.ua</c>). Match is exact, case-insensitive.</summary>
    public IReadOnlyList<string> AllowedDomains { get; init; } = Array.Empty<string>();

    /// <summary>Specific email addresses permitted for registration. Match is exact, case-insensitive.</summary>
    public IReadOnlyList<string> AllowedEmails { get; init; } = Array.Empty<string>();

    /// <summary>True when the allow-list is configured (either list non-empty).</summary>
    public bool HasAllowList => AllowedDomains.Count > 0 || AllowedEmails.Count > 0;

    /// <summary>Returns true when <paramref name="email"/> is permitted by the configured allow-list.</summary>
    public bool IsAllowed(string email)
    {
        if (!HasAllowList) return true;
        if (string.IsNullOrWhiteSpace(email)) return false;

        var normalised = email.Trim();
        foreach (var allowed in AllowedEmails)
        {
            if (string.Equals(normalised, allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        var atIndex = normalised.LastIndexOf('@');
        if (atIndex < 0 || atIndex == normalised.Length - 1) return false;
        var domain = normalised[(atIndex + 1)..];
        foreach (var allowed in AllowedDomains)
        {
            if (string.Equals(domain, allowed, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
