namespace CareerForge.Application.Auth.Dtos;

/// <summary>Token pair returned after a successful login, registration, or refresh.</summary>
public sealed record AuthResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt);
