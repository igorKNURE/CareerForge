namespace CareerForge.Application.Abstractions.BackgroundJobs;

/// <summary>
/// Fire-and-forget job queue. Producers enqueue a unit of work; a hosted consumer
/// drains the queue on a worker thread. Each job receives a fresh dependency-injection
/// scope, allowing safe resolution of scoped services such as DbContext.
/// </summary>
/// <remarks>
/// The default implementation is in-process. The interface is the seam at which a
/// distributed queue (e.g. Redis Streams, AWS SQS) can be substituted without
/// modification to call sites.
/// </remarks>
public interface IBackgroundJobQueue
{
    /// <summary>Enqueues a unit of work. Returns when the job has been accepted by the queue.</summary>
    ValueTask EnqueueAsync(Func<IServiceProvider, CancellationToken, Task> work, CancellationToken cancellationToken = default);

    /// <summary>Awaits and returns the next available job, or throws when the token is cancelled.</summary>
    ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken);
}
