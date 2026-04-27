namespace CareerForge.Infrastructure.Email;

/// <summary>Resend API credentials. <see cref="ApiKey"/> is required when the email provider is <c>resend</c>.</summary>
public sealed class ResendOptions
{
    public const string SectionName = "Email:Resend";

    public string ApiKey { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = "https://api.resend.com";
}
