using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace CondoLink.Api.Common;

public static class OpenAiResilience
{
    public static IHttpResiliencePipelineBuilder AddOpenAiResilience(
        this IHttpClientBuilder builder, string pipelineName) =>
        builder.AddResilienceHandler(pipelineName, static pipeline => pipeline
            .AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = 2,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = true,
                OnRetry = static _ =>
                {
                    OpenAiRetryTracker.Current?.Increment();
                    return default;
                },
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 8,
                FailureRatio = 0.5,
                BreakDuration = TimeSpan.FromSeconds(15),
            }));
}

public sealed class OpenAiRetryTracker : IDisposable
{
    private static readonly AsyncLocal<OpenAiRetryTracker?> CurrentTracker = new();
    private readonly OpenAiRetryTracker? previous;
    private int retryCount;

    private OpenAiRetryTracker()
    {
        previous = CurrentTracker.Value;
        CurrentTracker.Value = this;
    }

    public static OpenAiRetryTracker? Current => CurrentTracker.Value;
    public int RetryCount => Volatile.Read(ref retryCount);
    public static OpenAiRetryTracker Begin() => new();
    internal void Increment() => Interlocked.Increment(ref retryCount);
    public void Dispose() => CurrentTracker.Value = previous;
}
