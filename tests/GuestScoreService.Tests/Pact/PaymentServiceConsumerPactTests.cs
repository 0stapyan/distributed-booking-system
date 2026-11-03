using System.Net;
using GuestScoreService.Models;
using GuestScoreService.Services;
using GuestScoreService.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using PactNet;
using Xunit;

namespace GuestScoreService.Tests.Pact;

public class PaymentServiceConsumerPactTests
{
    private readonly IPactBuilderV3 _pact;

    public PaymentServiceConsumerPactTests()
    {
        var config = new PactConfig
        {
            PactDir = "../../../../../pacts",
            LogLevel = PactLogLevel.Warn,
        };
        _pact = PactNet.Pact.V3("GuestScoreService", "PaymentService", config).WithHttpInteractions();
    }

    [Fact]
    public async Task GetStats_WithElevatedFailureRate_DrivesTheScorePenalty()
    {
        _pact
            .UponReceiving("a request for payment stats when failures are elevated")
            .Given("there are 10 payments, 4 of which failed")
            .WithRequest(HttpMethod.Get, "/api/internal/payments/stats")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new { total = 10, charged = 6, failed = 4 });

        await _pact.VerifyAsync(async ctx =>
        {
            var result = await BuildEvaluator(ctx.MockServerUri)
                .EvaluateAsync(new GuestScoreRequest(1, RoomId: 7, Amount: 0m), CancellationToken.None);

            Assert.Equal(10, result.Payment.TotalPayments);
            Assert.Equal(4, result.Payment.FailedPayments);
            Assert.Equal(40, result.Payment.FailureRatePercent);
            Assert.Contains(result.Factors, f => f.Contains("Elevated payment failure rate"));
        });
    }

    [Fact]
    public async Task GetStats_NoPaymentsYet_YieldsNoPenalty()
    {
        _pact
            .UponReceiving("a request for payment stats when no payments exist")
            .Given("there are no payments")
            .WithRequest(HttpMethod.Get, "/api/internal/payments/stats")
            .WillRespond()
            .WithStatus(HttpStatusCode.OK)
            .WithHeader("Content-Type", "application/json; charset=utf-8")
            .WithJsonBody(new { total = 0, charged = 0, failed = 0 });

        await _pact.VerifyAsync(async ctx =>
        {
            var result = await BuildEvaluator(ctx.MockServerUri)
                .EvaluateAsync(new GuestScoreRequest(1, RoomId: 7, Amount: 0m), CancellationToken.None);

            Assert.Equal(0, result.Payment.TotalPayments);
            Assert.Equal(0, result.Payment.FailureRatePercent);
            Assert.DoesNotContain(result.Factors, f => f.Contains("Elevated payment failure rate"));
        });
    }

    private static GuestScoreEvaluator BuildEvaluator(Uri paymentServiceUri)
    {
        var factory = new StubHttpClientFactory
        {
            ["guest-service"] = _ => StubHttpClientFactory.Json(
                $$"""{"guestId":1,"exists":true,"createdAt":"{{DateTime.UtcNow.AddYears(-2):O}}"}"""),
        };
        factory.SetRealClient("payment-service", paymentServiceUri);

        return new GuestScoreEvaluator(factory, NullLogger<GuestScoreEvaluator>.Instance);
    }
}