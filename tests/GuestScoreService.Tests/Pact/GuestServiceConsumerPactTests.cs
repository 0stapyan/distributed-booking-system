using System.Net;
using GuestScoreService.Models;
using GuestScoreService.Services;
using GuestScoreService.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using PactNet;
using Xunit;

namespace GuestScoreService.Tests.Pact;

public class GuestServiceConsumerPactTests
{
    private readonly IPactBuilderV3 _pact;

    public GuestServiceConsumerPactTests()
    {
        var config = new PactConfig
        {
            PactDir = "../../../../../pacts",
            LogLevel = PactLogLevel.Warn,
        };
        _pact = PactNet.Pact.V3("GuestScoreService", "GuestService", config).WithHttpInteractions();
    }

    [Fact]
    public async Task GetGuest_KnownEstablishedGuest_ReturnsExistsWithCreatedAt()
    {
        _pact
            .UponReceiving("a request for a guest that exists")
            .Given("guest 1 exists and was created on 2026-01-01")
            .WithRequest(HttpMethod.Get, "/api/internal/guests/1")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new { guestId = 1, exists = true, createdAt = "2026-01-01T00:00:00Z" });

        await _pact.VerifyAsync(async ctx =>
        {
            var result = await BuildEvaluator(ctx.MockServerUri)
                .EvaluateAsync(new GuestScoreRequest(1, RoomId: 7, Amount: 0m), CancellationToken.None);

            Assert.True(result.Guest.Exists);
            Assert.Equal(1, result.Guest.GuestId);
            Assert.True(result.Guest.AccountAgeDays > 0);
        });
    }

    [Fact]
    public async Task GetGuest_UnknownGuest_ReturnsExistsFalse()
    {
        _pact
            .UponReceiving("a request for a guest that does not exist")
            .Given("guest 9999 does not exist")
            .WithRequest(HttpMethod.Get, "/api/internal/guests/9999")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new { guestId = 9999, exists = false, createdAt = "1970-01-01T00:00:00Z" });

        await _pact.VerifyAsync(async ctx =>
        {
            var result = await BuildEvaluator(ctx.MockServerUri)
                .EvaluateAsync(new GuestScoreRequest(9999, RoomId: 7, Amount: 0m), CancellationToken.None);

            Assert.False(result.Guest.Exists);
            Assert.Contains(result.Factors, f => f.Contains("no record"));
        });
    }

    private static GuestScoreEvaluator BuildEvaluator(Uri guestServiceUri)
    {
        var factory = new StubHttpClientFactory
        {
            ["payment-service"] = _ => StubHttpClientFactory.Json("""{"total":0,"charged":0,"failed":0}"""),
        };
        factory.SetRealClient("guest-service", guestServiceUri);

        return new GuestScoreEvaluator(factory, NullLogger<GuestScoreEvaluator>.Instance);
    }
}