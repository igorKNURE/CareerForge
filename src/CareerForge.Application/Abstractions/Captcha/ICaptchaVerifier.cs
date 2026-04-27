namespace CareerForge.Application.Abstractions.Captcha;

/// <summary>
/// Server-side verification of a CAPTCHA token issued to the browser. Implementations
/// may be no-ops (development) or hit a live provider (Cloudflare Turnstile, hCaptcha,
/// Google reCAPTCHA, etc.).
/// </summary>
public interface ICaptchaVerifier
{
    /// <summary>
    /// Returns true if the supplied token was issued by the provider and has not been
    /// previously consumed. Implementations that have no provider configured return
    /// true unconditionally (open access).
    /// </summary>
    Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken = default);

    /// <summary>True when verification is actually enforced. Front-ends may use this to skip rendering the widget.</summary>
    bool IsEnabled { get; }
}
