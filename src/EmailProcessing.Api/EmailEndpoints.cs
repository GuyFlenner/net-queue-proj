using EmailProcessing.Core;

namespace EmailProcessing.Api;

public static class EmailEndpoints
{
    public static IEndpointRouteBuilder MapEmailEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/emails").WithTags("Emails");

        group.MapPost("/", async (
                SendEmailRequest request,
                IBackgroundTaskQueue queue,
                ILogger<Program> logger) =>
            {
                // Capture only the primitive fields we need — never the request record's
                // enclosing scope or any scoped/disposed DI service. ILogger<Program> is
                // backed by the singleton ILoggerFactory, so it's safe to outlive this request.
                var to = request.To;
                var subject = request.Subject;
                var body = request.Body;

                // IBackgroundTaskQueue.QueueAsync takes no CancellationToken by the task's own
                // fixed interface spec — this call preserves that exact interface rather than
                // inventing a different contract. A prior revision wrapped this in
                // .WaitAsync(httpContext.RequestAborted) to "free" a disconnected client faster,
                // but that only stops the caller from waiting on the enqueue — it does NOT
                // cancel the underlying Channel.Writer.WriteAsync, which keeps running and still
                // writes the item once a slot frees (verified empirically; an external review
                // caught this — three prior internal review passes missed it). That made the
                // fix misleading without being wrong, so it was removed: a full bounded channel
                // means this await genuinely waits for capacity (asynchronous backpressure, not
                // thread blocking). In production I'd expose real producer cancellation or use a
                // try-write/fast-fail path so a full queue returns 503/429 instead of waiting.
                await queue.QueueAsync(async cancellationToken =>
                {
                    await Task.Delay(Random.Shared.Next(2000, 5001), cancellationToken);
                    logger.LogInformation(
                        "Email sent to {To} with subject {Subject} ({BodyLength} chars)",
                        to,
                        subject,
                        body.Length);
                });

                // 202 Accepted immediately — the queued work above is not awaited.
                return Results.Accepted(uri: (string?)null, value: new SendEmailResponse("Queued"));
            })
            .AddEndpointFilter<ValidationFilter<SendEmailRequest>>()
            .WithName("SendEmail");

        return app;
    }
}
