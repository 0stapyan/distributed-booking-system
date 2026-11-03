using BookingService.Application.Resilience;
using Polly;
using Polly.Retry;
using System.Net;
using Xunit;

namespace BookingService.Tests.Resilience;

/// <summary>
/// U1 — Retry: attempt count.
/// Author: 0stapyan (Ostap Turash)
/// </summary>
public class RetryAttemptCountTests
{
    [Fact]
    public async Task Handler_fails_503_twice_then_200_pipeline_succeeds_with_exactly_3_invocations()
    {
        var invocations = 0;

        // Retry-only pipeline, zero delay so the test runs instantly.
        // MaxRetryAttempts mirrors the production constant so the count is meaningful.
        var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .HandleResult(r => (int)r.StatusCode >= 500),
                MaxRetryAttempts = GuestScoreResiliencePipelineFactory.MaxRetryAttempts,
                Delay = TimeSpan.Zero,
            })
            .Build();

        var result = await pipeline.ExecuteAsync(ct =>
        {
            invocations++;
            var status = invocations <= GuestScoreResiliencePipelineFactory.MaxRetryAttempts
                ? HttpStatusCode.ServiceUnavailable   // fail first MaxRetryAttempts times
                : HttpStatusCode.OK;                  // succeed on the final attempt
            return new ValueTask<HttpResponseMessage>(new HttpResponseMessage(status));
        }, CancellationToken.None);

        // 1 initial + MaxRetryAttempts(2) retries = 3 total invocations
        Assert.Equal(GuestScoreResiliencePipelineFactory.MaxRetryAttempts + 1, invocations);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
    }
}
