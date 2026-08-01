namespace CondoLink.Api.Features.WhatsApp;

public static class OpenAiDiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapOpenAiDiagnosticsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/diagnostics/openai", async (
                IOpenAiAudioDiagnostics diagnostics,
                CancellationToken cancellationToken) =>
                Results.Ok(await diagnostics.CheckAsync(cancellationToken)))
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Diagnostics")
            .WithSummary("Temporarily checks HTTPS connectivity to OpenAI");

        return endpoints;
    }
}
