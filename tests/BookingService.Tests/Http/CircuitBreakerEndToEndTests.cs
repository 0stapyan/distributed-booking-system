using BookingService.Application.Resilience;
using BookingService.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace BookingService.Tests.Http;

/// <summary>
/// I7 — Circuit breaker end-to-end (hard).
/// Stubbed downstream returns 500 continuously → after the threshold the CB opens,
/// calls fail fast without hitting the server, evaluator returns the degraded fail-open result.
/// Author: 0stapyan (Ostap Turash)
/// </summary>
public class CircuitBreakerEndToEndTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    [Fact]
    public async Task Continuous_500s_open_circuit_breaker_calls_fail_fast_evaluator_returns_degraded()
    {
        _server
            .Given(Request.Create().WithPath("/api/guest-score").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500));

        using var httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var pipeline = GuestScoreResiliencePipelineFactory.Create();
        var client = new GuestScoreClient(
            httpClient,
            new HttpContextAccessor(),
            pipeline,
            NullLogger<GuestScoreClient>.Instance);

        // Each EvaluateAsync drives 1 + MaxRetryAttempts = 3 requests through the CB.
        // CB opens when window has >= MinimumThroughput(4) calls with ratio >= 50%.
        // After 2 EvaluateAsync calls: ~4+ CB failures recorded → ratio=100% → CB opens.
        for (var i = 0; i < 2; i++)
        {
            await client.EvaluateAsync(guestId: i, roomId: 1, amount: 100m, CancellationToken.None);
        }

        var serverHitsBeforeFastFail = _server.LogEntries.Count();

        // With CB open the next call must NOT reach the server.
        var result = await client.EvaluateAsync(guestId: 99, roomId: 1, amount: 100m, CancellationToken.None);

        Assert.Equal(serverHitsBeforeFastFail, _server.LogEntries.Count()); // bounded — no new hit
        Assert.True(result.Approved);                                         // fail-open
        Assert.Equal("approved-fail-open", result.Decision);
    }
}
