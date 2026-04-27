namespace CareerForge.Application.Auth.Dtos;

/// <summary>Used to exchange a refresh token for a fresh access/refresh pair.</summary>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>Revokes the supplied refresh token.</summary>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>Request a password-reset link to be emailed to the given address.</summary>
public sealed record ForgotPasswordRequest(string Email);

/// <summary>Complete a password reset using the token delivered by email.</summary>
public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

/// <summary>Re-issues an email-confirmation link for the given address.</summary>
public sealed record SendEmailConfirmRequest(string Email);

/// <summary>Confirms the user's email using the token delivered by mail.</summary>
public sealed record ConfirmEmailRequest(string Email, string Token);
