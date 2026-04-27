using CareerForge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CareerForge.Infrastructure.Interview;

/// <summary>
/// On host startup, populates the question bank from <see cref="QuestionBankSeed"/> if it's empty.
/// Failures (e.g. DB unreachable in dev) are logged and swallowed so the host still comes up.
/// </summary>
public sealed class QuestionBankSeeder(
    IServiceProvider services,
    ILogger<QuestionBankSeeder> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var existing = await db.QuestionBank.CountAsync(cancellationToken);
            if (existing > 0)
            {
                logger.LogInformation("Question bank already seeded ({Count} entries)", existing);
                return;
            }

            db.QuestionBank.AddRange(QuestionBankSeed.All);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded {Count} question bank entries", QuestionBankSeed.All.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Question bank seeding skipped (DB may be unavailable)");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
