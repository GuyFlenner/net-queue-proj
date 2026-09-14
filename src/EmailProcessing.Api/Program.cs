using EmailProcessing.Api;
using EmailProcessing.Core;
using FluentValidation;

var builder = WebApplication.CreateBuilder(args);

// FluentValidation otherwise localizes messages off the host OS's culture (e.g. Hebrew on a
// he-IL machine) — force English so API error responses are locale-independent.
FluentValidation.ValidatorOptions.Global.LanguageManager.Enabled = false;

// Singleton by design: one channel-backed queue shared by every request and by the
// single background consumer. Never a static field — DI owns its lifetime.
builder.Services.AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue());
builder.Services.AddHostedService<QueuedTaskProcessingService>();

builder.Services.AddValidatorsFromAssemblyContaining<SendEmailRequestValidator>();
builder.Services.AddProblemDetails();

var app = builder.Build();

// AddProblemDetails() alone only registers the formatter — without UseExceptionHandler() an
// unhandled exception returns an empty-body 500 in Production (it only looks fine in
// Development, where DeveloperExceptionPageMiddleware happens to mask the gap).
app.UseExceptionHandler();

app.MapEmailEndpoints();

app.Run();

// Exposed so EmailProcessing.Tests can spin this app up in-process via WebApplicationFactory.
public partial class Program;
