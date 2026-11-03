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
/// I8 — HTTP timeout end-to-end.
/// Stubbed downstream delays past the 2s per-attempt timeout → Polly cuts the leg off;
/// the total request stays within the 5s TotalTimeout budget and the degraded result is returned.
/// Author: 0stapyan (Ostap Turash)
/// </summary>
public class TimeoutEndToEndTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    [Fact]
    public async Task Handler_slower_than_per_attempt_timeout_total_stays_within_budget_degraded_returned()
    {
        // 3s response delay exceeds PerAttemptTimeout (2s) on every attempt.
        _server
            .Given(Request.Create().WithPath("/api/guest-score").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithBody("{}")
                .WithDelay(TimeSpan.FromSeconds(3)));

        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(_server.Url!),
            Timeout = Timeout.InfiniteTimeSpan, // let Polly own all timeout decisions
        };
        var pipeline = GuestScoreResiliencePipelineFactory.Create();
        var client = new GuestScoreClient(
            httpClient,
            new HttpContextAccessor(),
            pipeline,
            NullLogger<GuestScoreClient>.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await client.EvaluateAsync(guestId: 1, roomId: 1, amount: 100m, CancellationToken.None);
        sw.Stop();

        // PerAttemptTimeout (2s) cuts each attempt before the 3s response arrives.
        // TotalTimeout (5s) caps the entire request including all retries.
        // Allow 2s of OS + scheduling overhead on top of the total budget.
        var ceiling = GuestScoreResiliencePipelineFactory.TotalTimeout + TimeSpan.FromSeconds(2);
        Assert.True(sw.Elapsed < ceiling,
            $"Elapsed {sw.Elapsed.TotalSeconds:F1}s exceeded ceiling {ceiling.TotalSeconds:F0}s — " +
            "TotalTimeout did not bound the request");

        // All attempts timed out → GuestScoreClient.EvaluateAsync catches and returns fail-open.
        Assert.True(result.Approved);
        Assert.Equal("approved-fail-open", result.Decision);
    }
}
