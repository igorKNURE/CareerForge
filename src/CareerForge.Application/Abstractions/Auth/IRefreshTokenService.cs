namespace CareerForge.Application.Abstractions.Auth;

/// <summary>
/// Issues, rotates, and revokes refresh tokens. Rotation is one-shot: a presented token is
/// invalidated and replaced atomically with a fresh one.
/// </summary>
public interface IRefreshTokenService
{
    Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RotationResult?> RotateAsync(string rawToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default);
}

/// <summary>A freshly minted refresh token (raw value, never hashed) and its expiry.</summary>
public sealed record RefreshTokenResult(string Token, DateTime ExpiresAt);

/// <summary>Result of a successful refresh-token rotation.</summary>
public sealed record RotationResult(Guid UserId, string NewToken, DateTime ExpiresAt);
