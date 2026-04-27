namespace CareerForge.Application.Auth.Dtos;

/// <summary>Payload submitted to the registration endpoint.</summary>
/// <param name="Email">Address to register, validated against the allow-list when configured.</param>
/// <param name="Password">Password subject to the configured complexity policy.</param>
/// <param name="DisplayName">Optional preferred name for greetings; trimmed when provided.</param>
/// <param name="CaptchaToken">CAPTCHA token from the client. Required when CAPTCHA verification is enforced.</param>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName = null,
    string? CaptchaToken = null);
