namespace CareerForge.Infrastructure.Captcha;

/// <summary>Cloudflare Turnstile site + secret keys.</summary>
public sealed class TurnstileOptions
{
    public const string SectionName = "Turnstile";

    /// <summary>Public site key, embedded in the frontend bundle. Optional in dev.</summary>
    public string SiteKey { get; init; } = string.Empty;

    /// <summary>Server-side secret used to verify tokens. Required when verification is enforced.</summary>
    public string SecretKey { get; init; } = string.Empty;

    public string VerifyUrl { get; init; } = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
}
