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
            })
            .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
            {
                SamplingDuration = TimeSpan.FromSeconds(30),
                MinimumThroughput = 8,
                FailureRatio = 0.5,
                BreakDuration = TimeSpan.FromSeconds(15),
            }));
}
