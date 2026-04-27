using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CareerForge.Application.Abstractions.Captcha;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Captcha;

/// <summary>
/// Cloudflare Turnstile token verifier
/// (<see href="https://developers.cloudflare.com/turnstile/get-started/server-side-validation/"/>).
/// Verification is skipped (returns true) when no secret key is configured, allowing
/// the same code path to run in development without the Turnstile widget rendered.
/// </summary>
public sealed class TurnstileVerifier(
    HttpClient httpClient,
    IOptions<TurnstileOptions> options,
    ILogger<TurnstileVerifier> logger)
    : ICaptchaVerifier
{
    public const string HttpClientName = "turnstile";

    public bool IsEnabled => !string.IsNullOrWhiteSpace(options.Value.SecretKey);

    public async Task<bool> VerifyAsync(string? token, string? remoteIp, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!IsEnabled) return true;

        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogDebug("Turnstile rejected: missing token");
            return false;
        }

        var form = new List<KeyValuePair<string, string>>
        {
            new("secret", opts.SecretKey),
            new("response", token),
        };
        if (!string.IsNullOrWhiteSpace(remoteIp))
            form.Add(new("remoteip", remoteIp));

        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await httpClient.PostAsync(opts.VerifyUrl, content, cancellationToken);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: cancellationToken);
            if (result?.Success == true) return true;

            logger.LogWarning(
                "Turnstile rejected token: errors={Errors}",
                result?.ErrorCodes is null ? "(none)" : string.Join(",", result.ErrorCodes));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Turnstile verification call failed");
            return false;
        }
    }

    private sealed record TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error-codes")]
        public IReadOnlyList<string>? ErrorCodes { get; init; }
    }
}
