namespace CareerForge.Application.Auth.Dtos;

/// <summary>Public profile of the currently authenticated user.</summary>
public sealed record MeResponse(Guid Id, string Email, string? DisplayName);
