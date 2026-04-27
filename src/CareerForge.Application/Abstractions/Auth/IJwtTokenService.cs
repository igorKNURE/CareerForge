namespace CareerForge.Application.Abstractions.Auth;

/// <summary>Mints short-lived signed JWT access tokens for authenticated users.</summary>
public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(Guid userId, string email, IEnumerable<string> roles);
}

/// <summary>An access token plus its absolute expiration.</summary>
public sealed record AccessTokenResult(string Token, DateTime ExpiresAt);
