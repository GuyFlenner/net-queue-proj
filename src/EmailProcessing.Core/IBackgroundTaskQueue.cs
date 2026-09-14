namespace EmailProcessing.Core;

/// <summary>
/// A queue of deferred, cancellable work items to be executed by a background worker.
/// Producers (e.g. API endpoints) enqueue work and return immediately; a single
/// background consumer (see <see cref="QueuedTaskProcessingService"/>) dequeues and
/// executes items sequentially. Implementations must be thread-safe for multiple
/// concurrent producers and must never block a thread synchronously.
/// </summary>
public interface IBackgroundTaskQueue
{
    ValueTask QueueAsync(Func<CancellationToken, Task> workItem);

    ValueTask<Func<CancellationToken, Task>> DequeueAsync(CancellationToken token);
}
