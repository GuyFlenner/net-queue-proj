using System.Collections.Concurrent;
using EmailProcessing.Core;

namespace EmailProcessing.Tests;

public class BackgroundTaskQueueTests
{
    [Fact]
    public async Task QueueAsync_FromManyConcurrentProducers_AllItemsAreEventuallyDequeuedExactlyOnce()
    {
        const int producerCount = 50;
        const int itemsPerProducer = 5;
        const int totalItems = producerCount * itemsPerProducer;

        var queue = new BackgroundTaskQueue(capacity: 100);
        var processed = new ConcurrentBag<int>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        // Single consumer, mirroring the real QueuedTaskProcessingService loop shape.
        var consumerTask = Task.Run(async () =>
        {
            while (processed.Count < totalItems)
            {
                var workItem = await queue.DequeueAsync(cts.Token);
                await workItem(cts.Token);
            }
        }, cts.Token);

        var producerTasks = Enumerable.Range(0, producerCount).Select(producerId => Task.Run(async () =>
        {
            for (var i = 0; i < itemsPerProducer; i++)
            {
                var itemId = (producerId * itemsPerProducer) + i;
                await queue.QueueAsync(_ =>
                {
                    processed.Add(itemId);
                    return Task.CompletedTask;
                });
            }
        }));

        await Task.WhenAll(producerTasks);
        await consumerTask;

        Assert.Equal(totalItems, processed.Count);
        Assert.Equal(Enumerable.Range(0, totalItems), processed.OrderBy(x => x));
    }

    [Fact]
    public async Task DequeueAsync_WhenTokenCancelledWhileQueueIsEmpty_ThrowsPromptlyInsteadOfHanging()
    {
        var queue = new BackgroundTaskQueue();
        using var cts = new CancellationTokenSource();

        var dequeueTask = queue.DequeueAsync(cts.Token).AsTask();

        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        var winner = await Task.WhenAny(dequeueTask, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(dequeueTask, winner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => dequeueTask);
    }

    [Fact]
    public async Task QueueAsync_NullWorkItem_Throws()
    {
        var queue = new BackgroundTaskQueue();

        await Assert.ThrowsAsync<ArgumentNullException>(() => queue.QueueAsync(null!).AsTask());
    }
}
