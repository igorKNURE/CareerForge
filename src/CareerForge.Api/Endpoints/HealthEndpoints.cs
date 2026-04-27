using CareerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CareerForge.Api.Endpoints;

/// <summary>
/// Liveness and readiness probes.
/// <list type="bullet">
///   <item><c>/health</c> — application-level liveness with a timestamp; surfaced in the UI.</item>
///   <item><c>/healthz</c> — Kubernetes-style liveness, returns 200 while the process is responsive.</item>
///   <item><c>/readyz</c>  — readiness with a database-connectivity probe; returns 503 when degraded.</item>
/// </list>
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok", timestamp = DateTime.UtcNow }))
            .WithTags("Health");

        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }))
            .WithTags("Health")
            .ExcludeFromDescription();

        app.MapGet("/readyz", async (AppDbContext db, CancellationToken ct) =>
        {
            try
            {
                var dbOk = await db.Database.CanConnectAsync(ct);
                return dbOk
                    ? Results.Ok(new { status = "ready", database = "ok" })
                    : Results.Json(new { status = "degraded", database = "unreachable" }, statusCode: 503);
            }
            catch (Exception ex)
            {
                return Results.Json(new { status = "degraded", database = "error", detail = ex.Message }, statusCode: 503);
            }
        })
        .WithTags("Health")
        .ExcludeFromDescription();

        return app;
    }
}
