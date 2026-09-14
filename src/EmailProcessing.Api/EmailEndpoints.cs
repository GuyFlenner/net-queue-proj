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
                ILogger<Program> logger,
                HttpContext httpContext) =>
            {
                // Capture only the primitive fields we need — never the request record's
                // enclosing scope or any scoped/disposed DI service. ILogger<Program> is
                // backed by the singleton ILoggerFactory, so it's safe to outlive this request.
                var to = request.To;
                var subject = request.Subject;
                var body = request.Body;

                // IBackgroundTaskQueue.QueueAsync takes no CancellationToken by the task's own
                // fixed interface spec — bound the enqueue wait here instead (via WaitAsync) so
                // a client disconnect while the bounded channel is full still frees this
                // request rather than parking it indefinitely on backpressure.
                await queue.QueueAsync(async cancellationToken =>
                {
                    await Task.Delay(Random.Shared.Next(2000, 5001), cancellationToken);
                    logger.LogInformation(
                        "Email sent to {To} with subject {Subject} ({BodyLength} chars)",
                        to,
                        subject,
                        body.Length);
                }).AsTask().WaitAsync(httpContext.RequestAborted);

                // 202 Accepted immediately — the queued work above is not awaited.
                return Results.Accepted(uri: (string?)null, value: new SendEmailResponse("Queued"));
            })
            .AddEndpointFilter<ValidationFilter<SendEmailRequest>>()
            .WithName("SendEmail");

        return app;
    }
}
