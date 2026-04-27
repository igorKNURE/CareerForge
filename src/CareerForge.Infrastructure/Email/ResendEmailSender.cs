using System.Net.Http.Headers;
using System.Net.Http.Json;
using CareerForge.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailSender"/> backed by the Resend HTTP API
/// (<see href="https://resend.com/docs/api-reference"/>). Authentication uses a single
/// API key supplied via configuration.
/// </summary>
public sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> emailOptions,
    IOptions<ResendOptions> resendOptions,
    ILogger<ResendEmailSender> logger)
    : IEmailSender
{
    public const string HttpClientName = "resend";

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var email = emailOptions.Value;
        var resend = resendOptions.Value;

        if (string.IsNullOrWhiteSpace(resend.ApiKey))
        {
            logger.LogWarning("Resend API key not configured; dropping email to {To}", message.ToEmail);
            return;
        }
        if (string.IsNullOrWhiteSpace(email.From))
        {
            logger.LogWarning("Email From address not configured; dropping email to {To}", message.ToEmail);
            return;
        }

        // Trim configured values defensively — env-var copy/paste sometimes introduces
        // surrounding whitespace, which Resend rejects as a malformed address.
        var fromAddress = email.From.Trim();
        var fromName = email.FromName?.Trim();
        var from = string.IsNullOrWhiteSpace(fromName)
            ? fromAddress
            : $"{fromName} <{fromAddress}>";

        // Recipient is sent as a bare address rather than RFC "Name <email>" format.
        // Resend's free-tier sandbox compares the recipient string against the verified
        // developer email; the angle-bracket form fails that comparison even when the
        // address is identical. Bare addresses pass.
        var payload = new
        {
            from,
            to = new[] { message.ToEmail },
            subject = message.Subject,
            text = message.TextBody,
            html = message.HtmlBody,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/emails")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resend.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError(
                "Resend send failed for {To}: {Status} {Body}",
                message.ToEmail, (int)response.StatusCode, body);
        }
    }
}
