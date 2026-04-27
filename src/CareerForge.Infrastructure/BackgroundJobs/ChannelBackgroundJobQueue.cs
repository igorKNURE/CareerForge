using System.Threading.Channels;
using CareerForge.Application.Abstractions.BackgroundJobs;

namespace CareerForge.Infrastructure.BackgroundJobs;

/// <summary>
/// In-process implementation of <see cref="IBackgroundJobQueue"/> backed by a bounded
/// <see cref="Channel{T}"/>. Producers apply backpressure when the queue is full rather
/// than dropping work.
/// </summary>
public sealed class ChannelBackgroundJobQueue : IBackgroundJobQueue
{
    private const int Capacity = 256;

    private readonly Channel<Func<IServiceProvider, CancellationToken, Task>> _channel =
        Channel.CreateBounded<Func<IServiceProvider, CancellationToken, Task>>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false,
            });

    public ValueTask EnqueueAsync(
        Func<IServiceProvider, CancellationToken, Task> work,
        CancellationToken cancellationToken = default)
        => _channel.Writer.WriteAsync(work, cancellationToken);

    public ValueTask<Func<IServiceProvider, CancellationToken, Task>> DequeueAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAsync(cancellationToken);
}
