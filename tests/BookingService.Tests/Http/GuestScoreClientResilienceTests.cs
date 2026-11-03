using BookingService.Application.Resilience;
using BookingService.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace BookingService.Tests.Http;

public class GuestScoreClientResilienceTests : IDisposable
{
    private readonly WireMockServer _server = WireMockServer.Start();

    public void Dispose() => _server.Stop();

    [Fact]
    public async Task Stubbed_downstream_503_twice_then_200_succeeds_transparently_via_polly()
    {
        _server
            .Given(Request.Create().WithPath("/api/guest-score").UsingPost())
            .InScenario("guest-score-flaky")
            .WillSetStateTo("second-attempt")
            .RespondWith(Response.Create().WithStatusCode(503));

        _server
            .Given(Request.Create().WithPath("/api/guest-score").UsingPost())
            .InScenario("guest-score-flaky")
            .WhenStateIs("second-attempt")
            .WillSetStateTo("third-attempt")
            .RespondWith(Response.Create().WithStatusCode(503));

        _server
            .Given(Request.Create().WithPath("/api/guest-score").UsingPost())
            .InScenario("guest-score-flaky")
            .WhenStateIs("third-attempt")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBodyAsJson(new
                {
                    Approved = true,
                    Score = 80,
                    Decision = "approved",
                    Factors = new[] { "All checks passed" },
                }));

        using var httpClient = new HttpClient { BaseAddress = new Uri(_server.Url!) };
        var pipeline = GuestScoreResiliencePipelineFactory.Create();
        var client = new GuestScoreClient(
            httpClient,
            new HttpContextAccessor(),
            pipeline,
            NullLogger<GuestScoreClient>.Instance);

        var decision = await client.EvaluateAsync(guestId: 42, roomId: 7, amount: 250m, CancellationToken.None);

        Assert.True(decision.Approved);
        Assert.Equal("approved", decision.Decision);
        Assert.Equal(3, _server.LogEntries.Count());
    }
}
