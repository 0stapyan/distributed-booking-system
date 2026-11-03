using GuestScoreService.Models;
using GuestScoreService.Services;
using GuestScoreService.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GuestScoreService.Tests.Evaluator;

public class EvaluatorPartialFailureTests
{
    [Fact]
    public async Task One_downstream_throws_other_succeeds_composes_degraded_result_without_total_failure()
    {
        var factory = new StubHttpClientFactory
        {
            ["guest-service"] = _ => throw new HttpRequestException("guest-service unreachable"),
            ["payment-service"] = _ => StubHttpClientFactory.Json("""{"total":10,"charged":6,"failed":4}"""),
        };

        var evaluator = new GuestScoreEvaluator(factory, NullLogger<GuestScoreEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(new GuestScoreRequest(42, 7, 0m), CancellationToken.None);

        Assert.Equal(10, result.Payment.TotalPayments);
        Assert.Equal(4, result.Payment.FailedPayments);
        Assert.Equal(40, result.Payment.FailureRatePercent);

        Assert.Equal(42, result.Guest.GuestId);
        Assert.False(result.Guest.Exists);

        Assert.Equal(30, result.Score);
        Assert.False(result.Approved);
        Assert.Equal("prepayment-required", result.Decision);
        Assert.Contains(result.Factors, f => f.Contains("no record"));
        Assert.Contains(result.Factors, f => f.Contains("Elevated payment failure rate"));

        Assert.Equal(1, factory.CallCounts["guest-service"]);
        Assert.Equal(1, factory.CallCounts["payment-service"]);
    }

    [Fact]
    public async Task Both_downstreams_throw_still_returns_a_result_rather_than_rethrowing()
    {
        var factory = new StubHttpClientFactory
        {
            ["guest-service"] = _ => throw new HttpRequestException("guest-service unreachable"),
            ["payment-service"] = _ => throw new HttpRequestException("payment-service unreachable"),
        };

        var evaluator = new GuestScoreEvaluator(factory, NullLogger<GuestScoreEvaluator>.Instance);

        var result = await evaluator.EvaluateAsync(new GuestScoreRequest(42, 7, 0m), CancellationToken.None);

        Assert.False(result.Guest.Exists);
        Assert.Equal(0, result.Payment.TotalPayments);
        Assert.Equal(50, result.Score);
    }
}