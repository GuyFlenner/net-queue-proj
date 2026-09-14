using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EmailProcessing.Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EmailProcessing.Tests;

/// <summary>
/// Runs the real ASP.NET Core pipeline in-process (WebApplicationFactory) — DI wiring, the
/// validation filter, routing — against the singleton in-memory queue, no external service.
/// </summary>
public class EmailEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PostEmails_WithValidBody_ReturnsAcceptedQueuedWithoutAwaitingTheQueuedWork()
    {
        var request = new SendEmailRequest("person@example.com", "Hi", "Hello there");

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.PostAsJsonAsync("/api/emails", request);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // The queued work sleeps 2-5s before "sending" — a response in well under that proves
        // the endpoint returned immediately instead of awaiting the queued work item.
        Assert.True(
            stopwatch.ElapsedMilliseconds < 500,
            $"expected the response in well under 500ms, took {stopwatch.ElapsedMilliseconds}ms");

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Queued", body.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData("", "Subject", "Body")]
    [InlineData("not-an-email", "Subject", "Body")]
    [InlineData("person@example.com", "", "Body")]
    [InlineData("person@example.com", "Subject", "")]
    public async Task PostEmails_WithInvalidBody_ReturnsBadRequest(string to, string subject, string body)
    {
        var request = new SendEmailRequest(to, subject, body);

        var response = await _client.PostAsJsonAsync("/api/emails", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
