using CareerForge.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace CareerForge.Infrastructure.Email;

/// <summary>
/// No-op <see cref="IEmailSender"/> used in development and as a fallback when no
/// provider is configured. Logs the message body at Information level rather than
/// dispatching it.
/// </summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Email (not delivered) — to={To}, subject={Subject}, body={Body}",
            message.ToEmail, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}
