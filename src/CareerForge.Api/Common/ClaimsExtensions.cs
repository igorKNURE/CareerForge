using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace CareerForge.Api.Common;

/// <summary>Helpers for reading well-known claims off the current <see cref="ClaimsPrincipal"/>.</summary>
public static class ClaimsExtensions
{
    /// <summary>
    /// Returns the authenticated user's id from the standard NameIdentifier or JWT <c>sub</c> claim.
    /// Throws <see cref="UnauthorizedAccessException"/> if the claim is missing or malformed.
    /// </summary>
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var id))
            throw new UnauthorizedAccessException("Token does not contain a valid user id.");
        return id;
    }
}
