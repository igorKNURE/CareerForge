using CareerForge.Application.Abstractions.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CareerForge.Infrastructure.BackgroundJobs;

/// <summary>
/// Hosted service that drains <see cref="IBackgroundJobQueue"/> on a dedicated worker.
/// Each job executes within an isolated dependency-injection scope, allowing safe
/// resolution of scoped services. Exceptions thrown by a job are logged and isolated
/// from subsequent jobs.
/// </summary>
public sealed class BackgroundJobHostedService(
    IBackgroundJobQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<BackgroundJobHostedService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Background job worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            Func<IServiceProvider, CancellationToken, Task> work;
            try
            {
                work = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                await work(scope.ServiceProvider, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background job failed");
            }
        }
        logger.LogInformation("Background job worker stopped");
    }
}
