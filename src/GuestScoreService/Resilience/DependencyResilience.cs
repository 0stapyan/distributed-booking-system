using Microsoft.Extensions.Http.Resilience;
using Polly;
using Polly.Retry;

namespace GuestScoreService.Resilience;

public static class DependencyResilience
{
    public static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan TotalRequestTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(200);
    public const int MaxRetryAttempts = 3;
    public const double CircuitBreakerFailureRatio = 0.5;
    public const int CircuitBreakerMinimumThroughput = 5;
    public static readonly TimeSpan CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan CircuitBreakerBreakDuration = TimeSpan.FromSeconds(15);

    public static void Configure(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        builder
            .AddTimeout(TotalRequestTimeout)
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = BaseRetryDelay,
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                FailureRatio = CircuitBreakerFailureRatio,
                MinimumThroughput = CircuitBreakerMinimumThroughput,
                SamplingDuration = CircuitBreakerSamplingDuration,
                BreakDuration = CircuitBreakerBreakDuration,
            })
            .AddTimeout(PerAttemptTimeout);
    }
}