using BookingService.Application.Resilience;
using Polly;
using Polly.CircuitBreaker;
using Xunit;

namespace BookingService.Tests.Resilience;

public class GuestScoreCircuitBreakerTests
{
    [Fact]
    public async Task Opens_after_minimum_throughput_failures_and_fails_fast_without_invoking_handler()
    {
        var invocationCount = 0;
        var stateProvider = new CircuitBreakerStateProvider();

        var pipeline = new ResiliencePipelineBuilder<string>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<string>
            {
                ShouldHandle = new PredicateBuilder<string>().Handle<InvalidOperationException>(),
                FailureRatio = GuestScoreResiliencePipelineFactory.CircuitBreakerFailureRatio,
                MinimumThroughput = GuestScoreResiliencePipelineFactory.CircuitBreakerMinimumThroughput,
                SamplingDuration = GuestScoreResiliencePipelineFactory.CircuitBreakerSamplingDuration,
                BreakDuration = GuestScoreResiliencePipelineFactory.CircuitBreakerBreakDuration,
                StateProvider = stateProvider,
            })
            .Build();

        Task<string> FailingHandler(CancellationToken ct)
        {
            invocationCount++;
            throw new InvalidOperationException("payment-service unavailable");
        }

        for (var i = 0; i < GuestScoreResiliencePipelineFactory.CircuitBreakerMinimumThroughput; i++)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => pipeline.ExecuteAsync(ct => new ValueTask<string>(FailingHandler(ct))).AsTask());
        }

        Assert.Equal(GuestScoreResiliencePipelineFactory.CircuitBreakerMinimumThroughput, invocationCount);
        Assert.Equal(CircuitState.Open, stateProvider.CircuitState);

        var countBeforeTrip = invocationCount;
        await Assert.ThrowsAsync<BrokenCircuitException>(
            () => pipeline.ExecuteAsync(ct => new ValueTask<string>(FailingHandler(ct))).AsTask());

        Assert.Equal(countBeforeTrip, invocationCount);
    }
}
