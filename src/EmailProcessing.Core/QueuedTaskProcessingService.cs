using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EmailProcessing.Core;

/// <summary>
/// Continuously dequeues and executes work items from an <see cref="IBackgroundTaskQueue"/>.
/// A single sequential consumer loop is the simplest, safest default for the stated scope:
/// no shared mutable state between items, no need to reason about concurrent execution or
/// per-item cancellation ordering. (If throughput ever demanded it, the same loop could be
/// fanned out into N parallel consumer tasks reading the same channel — Channel&lt;T&gt;
/// readers are safe for multiple concurrent consumers — but that's out of scope here.)
/// Each item runs in its own try/catch so a throwing work item is logged and skipped, never
/// fatal to the host or to subsequently queued items. Retries are explicitly out of scope.
/// </summary>
public sealed class QueuedTaskProcessingService(
    IBackgroundTaskQueue queue,
    ILogger<QueuedTaskProcessingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Func<CancellationToken, Task> workItem;
            try
            {
                workItem = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown: nothing was dequeued, so nothing was aborted mid-flight.
                break;
            }

            var workItemId = Guid.NewGuid();
            logger.LogInformation("Work item {WorkItemId} starting", workItemId);

            try
            {
                await workItem(stoppingToken);
                logger.LogInformation("Work item {WorkItemId} finished", workItemId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // The item itself observed shutdown mid-execution and stopped cooperatively —
                // expected during a graceful shutdown, not a failure worth logging as an error.
                logger.LogWarning("Work item {WorkItemId} cancelled by host shutdown", workItemId);
            }
            catch (Exception ex)
            {
                // Deliberately not rethrown: one throwing work item must not crash the host or
                // stop subsequent items from being dequeued and processed. Logged at Error with
                // full exception detail so this is "handled", not "silently swallowed".
                logger.LogError(ex, "Work item {WorkItemId} failed", workItemId);
            }
        }
    }
}
