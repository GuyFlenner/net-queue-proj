# Queue-Based Background Email Processing (.NET 8)

Minimal Email Sending API: `POST /api/emails` validates the request, enqueues the send, and
returns `202 Accepted` immediately — the actual "send" (simulated 2–5s delay, log only) runs
asynchronously on a background worker, never inside the HTTP request.

## How to run

Requires the .NET 8 SDK.

```bash
dotnet test          # 12/12, 93.6% line coverage — concurrency, cancellation, backpressure,
                      # and exception-isolation are all directly tested, not just the happy path
dotnet run --project src/EmailProcessing.Api
```

Architecture diagram (3 tabs — request/processing walkthrough, component architecture,
deployment topology): [`docs/architecture.drawio`](docs/architecture.drawio) (open in
[app.diagrams.net](https://app.diagrams.net) or the VS Code draw.io extension).

The API listens on the URL printed at startup (e.g. `http://localhost:5000`).

## Example request

```bash
curl -i http://localhost:5000/api/emails \
  -X POST -H "Content-Type: application/json" \
  -d '{"to":"guy@example.com","subject":"Hello","body":"Test"}'
```

Response (returns in well under a millisecond — before the email is actually "sent"):

```
HTTP/1.1 202 Accepted
Content-Type: application/json

{"status":"Queued"}
```

Invalid input (empty/malformed `to`, empty `subject`/`body`) returns `400 Bad Request` with
FluentValidation's standard `ValidationProblem` body.

## Expected log output

```
info: Microsoft.AspNetCore.Hosting.Diagnostics[2]
      Request finished HTTP/1.1 POST http://localhost:5000/api/emails - 202 - application/json 0.7ms
info: EmailProcessing.Core.QueuedTaskProcessingService[0]
      Work item 634c551c-e4f7-4864-aa19-eecae6199c43 starting
info: Program[0]
      Email sent to guy@example.com with subject Hello (4 chars)
info: EmailProcessing.Core.QueuedTaskProcessingService[0]
      Work item 634c551c-e4f7-4864-aa19-eecae6199c43 finished
```

The HTTP request completes immediately; the "starting" / "finished" pair for the same work-item
id appears 2–5 seconds later, on the background worker, after the simulated delay.

## Key design decisions (brief — full reasoning defended live)

- **`Channel<Func<CancellationToken,Task>>`** backs `IBackgroundTaskQueue`, over a hand-rolled
  `ConcurrentQueue` + `SemaphoreSlim`: same guarantees, less custom synchronization code to get
  wrong.
- **Bounded (capacity 100), `BoundedChannelFullMode.Wait`**: a full queue makes a producer's
  `QueueAsync` asynchronously await a free slot rather than growing memory without limit — a
  deliberate backpressure choice, not an oversight.
- **Single sequential consumer** in the `BackgroundService`: simplest correct answer for this
  scope (no cross-item shared state to reason about). The same `Channel<T>` reader supports
  multiple concurrent consumers if throughput ever demanded it.
- **Per-item `try`/`catch`** in the worker loop: one throwing work item is logged at `Error` and
  skipped; it never crashes the host or blocks subsequently queued items.
- **No static state**: the queue and worker are DI singleton / hosted-service instances only.
