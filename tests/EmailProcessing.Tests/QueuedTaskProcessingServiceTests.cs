using EmailProcessing.Core;
using Microsoft.Extensions.Logging.Abstractions;

namespace EmailProcessing.Tests;

/// <summary>
/// Runs the real <see cref="QueuedTaskProcessingService"/> BackgroundService (not just the
/// queue) to prove a throwing work item is isolated per-item: it must not crash the host or
/// prevent the next queued item from being dequeued and processed.
/// </summary>
public class QueuedTaskProcessingServiceTests
{
    [Fact]
    public async Task ExecuteAsync_WhenAWorkItemThrows_StillProcessesTheNextQueuedItem()
    {
        var queue = new BackgroundTaskQueue();
        var service = new QueuedTaskProcessingService(queue, NullLogger<QueuedTaskProcessingService>.Instance);

        var secondItemProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await queue.QueueAsync(_ => throw new InvalidOperationException("boom — simulated failing work item"));
        await queue.QueueAsync(_ =>
        {
            secondItemProcessed.SetResult();
            return Task.CompletedTask;
        });

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        try
        {
            var winner = await Task.WhenAny(secondItemProcessed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(secondItemProcessed.Task, winner);
        }
        finally
        {
            cts.Cancel();
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StopAsync_RequestedWhileQueueIsEmpty_StopsTheHostCleanly()
    {
        var queue = new BackgroundTaskQueue();
        var service = new QueuedTaskProcessingService(queue, NullLogger<QueuedTaskProcessingService>.Instance);

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);

        cts.Cancel();

        var stopTask = service.StopAsync(CancellationToken.None);
        var winner = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(stopTask, winner);
    }

    [Fact]
    public async Task StopAsync_RequestedWhileItemsAreStillQueued_StopsPromptlyAndNeverRunsTheBacklog()
    {
        var queue = new BackgroundTaskQueue();
        var service = new QueuedTaskProcessingService(queue, NullLogger<QueuedTaskProcessingService>.Instance);

        var firstItemStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstItem = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterItemsRan = 0;

        // First item blocks until released, so the second/third are guaranteed to still be
        // sitting undequeued in the channel when shutdown is requested.
        await queue.QueueAsync(async ct =>
        {
            firstItemStarted.SetResult();
            await releaseFirstItem.Task.WaitAsync(ct);
        });
        await queue.QueueAsync(_ => { Interlocked.Increment(ref laterItemsRan); return Task.CompletedTask; });
        await queue.QueueAsync(_ => { Interlocked.Increment(ref laterItemsRan); return Task.CompletedTask; });

        using var cts = new CancellationTokenSource();
        await service.StartAsync(cts.Token);
        await firstItemStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cts.Cancel();
        releaseFirstItem.TrySetCanceled(cts.Token);

        var stopTask = service.StopAsync(CancellationToken.None);
        var winner = await Task.WhenAny(stopTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(stopTask, winner);
        Assert.Equal(0, laterItemsRan);
    }
}
