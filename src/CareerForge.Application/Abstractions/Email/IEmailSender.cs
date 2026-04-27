namespace CareerForge.Application.Abstractions.Email;

/// <summary>
/// Transactional email delivery. Implementations may be no-ops (development) or
/// production providers (Resend, SendGrid, etc.).
/// </summary>
public interface IEmailSender
{
    /// <summary>Sends a transactional email. Returns once the provider has accepted the request.</summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

/// <summary>Plain + HTML body for a single recipient.</summary>
public sealed record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string TextBody,
    string? HtmlBody = null);
