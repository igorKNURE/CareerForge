namespace CareerForge.Application.Auth.Dtos;

/// <summary>Payload submitted to the registration endpoint.</summary>
public sealed record RegisterRequest(string Email, string Password, string? DisplayName = null);
