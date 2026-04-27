namespace CareerForge.Application.Auth.Dtos;

/// <summary>Credentials submitted to the login endpoint.</summary>
public sealed record LoginRequest(string Email, string Password);
