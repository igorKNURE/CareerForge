using System.Security.Cryptography;
using System.Text;
using CareerForge.Application.Abstractions.Auth;
using CareerForge.Domain.Entities;
using CareerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CareerForge.Infrastructure.Auth;

/// <summary>
/// Persists refresh tokens as SHA-256 hashes (raw value never stored) and rotates them
/// in a single transaction so a presented token cannot be reused.
/// </summary>
public sealed class RefreshTokenService(
    AppDbContext db,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider)
    : IRefreshTokenService
{
    private readonly TimeSpan _lifetime = TimeSpan.FromDays(jwtOptions.Value.RefreshTokenLifetimeDays);

    public async Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var raw = GenerateRawToken();
        var hash = Hash(raw);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = hash,
            CreatedAt = now,
            ExpiresAt = now.Add(_lifetime),
        };
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new RefreshTokenResult(raw, entity.ExpiresAt);
    }

    public async Task<RotationResult?> RotateAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        var now = timeProvider.GetUtcNow().UtcDateTime;
        if (existing is null || !existing.IsActive(now))
            return null;

        var newRaw = GenerateRawToken();
        var newHash = Hash(newRaw);
        var newEntity = new RefreshToken
        {
            UserId = existing.UserId,
            TokenHash = newHash,
            CreatedAt = now,
            ExpiresAt = now.Add(_lifetime),
        };
        existing.RevokedAt = now;
        existing.ReplacedByTokenHash = newHash;
        db.RefreshTokens.Add(newEntity);
        await db.SaveChangesAsync(cancellationToken);
        return new RotationResult(existing.UserId, newRaw, newEntity.ExpiresAt);
    }

    public async Task RevokeAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return;
        var hash = Hash(rawToken);
        var existing = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (existing is null || existing.RevokedAt is not null) return;
        existing.RevokedAt = timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    private static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}
