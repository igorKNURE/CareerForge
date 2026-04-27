namespace CareerForge.Infrastructure.Email;

/// <summary>Top-level email configuration shared by all providers.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>One of <c>resend</c>, <c>log</c>. Defaults to <c>log</c> when unset.</summary>
    public string Provider { get; init; } = "log";

    /// <summary>Sender address, e.g. <c>noreply@careerforge.app</c>. Required for non-log providers.</summary>
    public string From { get; init; } = string.Empty;

    /// <summary>Optional sender display name, e.g. <c>CareerForge</c>.</summary>
    public string FromName { get; init; } = "CareerForge";
}
