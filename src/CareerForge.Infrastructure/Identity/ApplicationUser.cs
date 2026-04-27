using Microsoft.AspNetCore.Identity;

namespace CareerForge.Infrastructure.Identity;

/// <summary>The application's ASP.NET Identity user, keyed by <see cref="Guid"/>.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional preferred name supplied at registration; used for greetings.
    /// </summary>
    public string? DisplayName { get; set; }
}
