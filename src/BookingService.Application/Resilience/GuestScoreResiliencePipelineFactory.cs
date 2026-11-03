using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace BookingService.Application.Resilience;

public static class GuestScoreResiliencePipelineFactory
{
    public static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan PerAttemptTimeout = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan BaseRetryDelay = TimeSpan.FromMilliseconds(200);
    public const int MaxRetryAttempts = 2;
    public const double CircuitBreakerFailureRatio = 0.5;
    public const int CircuitBreakerMinimumThroughput = 4;
    public static readonly TimeSpan CircuitBreakerSamplingDuration = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan CircuitBreakerBreakDuration = TimeSpan.FromSeconds(15);

    public static ResiliencePipeline<HttpResponseMessage> Create(ILoggerFactory? loggerFactory = null)
    {
        var shouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>()
            .HandleResult(response => (int)response.StatusCode >= 500);

        var builder = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(TotalTimeout)
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = shouldHandle,
                MaxRetryAttempts = MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                Delay = BaseRetryDelay,
                UseJitter = true,
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = shouldHandle,
                FailureRatio = CircuitBreakerFailureRatio,
                MinimumThroughput = CircuitBreakerMinimumThroughput,
                SamplingDuration = CircuitBreakerSamplingDuration,
                BreakDuration = CircuitBreakerBreakDuration,
            })
            .AddTimeout(PerAttemptTimeout);

        builder.ConfigureTelemetry(loggerFactory ?? NullLoggerFactory.Instance);

        return builder.Build();
    }
}
