using System.Threading.Channels;

namespace EmailProcessing.Core;

/// <summary>
/// <see cref="Channel{T}"/>-backed <see cref="IBackgroundTaskQueue"/>. The channel is
/// <b>bounded</b> (default capacity 100) with <see cref="BoundedChannelFullMode.Wait"/>:
/// when full, a producer's <see cref="QueueAsync"/> call asynchronously awaits a free
/// slot instead of blocking a thread synchronously or dropping work silently — this is
/// a deliberate backpressure choice (bounded memory beats an unbounded queue that can
/// grow without limit), not an oversight. Safe for multiple concurrent producers and a
/// single consumer; registered as a DI singleton (see Program.cs), never a static field.
/// </summary>
public sealed class BackgroundTaskQueue : IBackgroundTaskQueue
{
    private readonly Channel<Func<CancellationToken, Task>> _channel;

    public BackgroundTaskQueue(int capacity = 100)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        }

        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        };

        _channel = Channel.CreateBounded<Func<CancellationToken, Task>>(options);
    }

    public ValueTask QueueAsync(Func<CancellationToken, Task> workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return _channel.Writer.WriteAsync(workItem);
    }

    public ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken token) =>
        _channel.Reader.ReadAsync(token);
}
