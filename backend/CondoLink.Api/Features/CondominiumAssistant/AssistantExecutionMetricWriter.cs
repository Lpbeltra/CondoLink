using System.Diagnostics;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumAssistant;

public sealed class AssistantExecutionMeasurement(Guid executionId, Guid condominiumId, DateTime startedAt)
{
    public Guid ExecutionId { get; } = executionId;
    public Guid CondominiumId { get; } = condominiumId;
    public DateTime StartedAt { get; } = startedAt;
    public long? RetrievalDurationMs { get; set; } public long? ExpansionDurationMs { get; set; }
    public long? EmbeddingDurationMs { get; set; } public long? DatabaseMaterializationDurationMs { get; set; }
    public long? EmbeddingDeserializationDurationMs { get; set; } public long? VectorScoringDurationMs { get; set; }
    public long? LexicalScoringDurationMs { get; set; } public long? RerankDurationMs { get; set; }
    public long? RerankFallbackDurationMs { get; set; } public long? ContextPreparationDurationMs { get; set; }
    public long? ChatDurationMs { get; set; } public long? TimeToFirstTokenMs { get; set; } public long? GenerationDurationMs { get; set; }
    public int? EligibleDocuments { get; set; } public int? EligibleChunks { get; set; } public int? LoadedChunks { get; set; }
    public int? DeserializedEmbeddings { get; set; } public int? ExpandedQueries { get; set; } public int? CandidatesBeforeRerank { get; set; }
    public int? CandidatesAfterRerank { get; set; } public int? FinalChunks { get; set; } public int? ContextCharacters { get; set; }
    public string? EmbeddingModel { get; set; } public string? ChatModel { get; set; }
    public bool RerankFallbackUsed { get; set; }
    public AssistantExecutionMetricValues Values(bool success, string? errorCategory) => new()
    {
        TotalDurationMs = (long)(DateTime.UtcNow - StartedAt).TotalMilliseconds, RetrievalDurationMs = RetrievalDurationMs,
        ExpansionDurationMs = ExpansionDurationMs, EmbeddingDurationMs = EmbeddingDurationMs,
        DatabaseMaterializationDurationMs = DatabaseMaterializationDurationMs, EmbeddingDeserializationDurationMs = EmbeddingDeserializationDurationMs,
        VectorScoringDurationMs = VectorScoringDurationMs, LexicalScoringDurationMs = LexicalScoringDurationMs,
        RerankDurationMs = RerankDurationMs, RerankFallbackDurationMs = RerankFallbackDurationMs,
        ContextPreparationDurationMs = ContextPreparationDurationMs, ChatDurationMs = ChatDurationMs,
        TimeToFirstTokenMs = TimeToFirstTokenMs, GenerationDurationMs = GenerationDurationMs,
        EligibleDocuments = EligibleDocuments, EligibleChunks = EligibleChunks, LoadedChunks = LoadedChunks,
        DeserializedEmbeddings = DeserializedEmbeddings, ExpandedQueries = ExpandedQueries,
        CandidatesBeforeRerank = CandidatesBeforeRerank, CandidatesAfterRerank = CandidatesAfterRerank,
        FinalChunks = FinalChunks, ContextCharacters = ContextCharacters, EmbeddingModel = EmbeddingModel,
        ChatModel = ChatModel, RerankFallbackUsed = RerankFallbackUsed, ErrorCategory = errorCategory,
        ErrorCode = errorCategory
    };
}

/// <summary>Uses an independent scope: observability writes never poison assistant response DbContext.</summary>
public sealed class AssistantExecutionMetricWriter(IServiceScopeFactory scopes, ILogger<AssistantExecutionMetricWriter> logger)
{
    public async Task WriteAsync(AssistantExecutionMeasurement measurement, bool success, Exception? exception = null)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var metric = new AssistantExecutionMetric(measurement.ExecutionId, measurement.CondominiumId, measurement.StartedAt);
            metric.Complete(DateTime.UtcNow, success, measurement.Values(success, success ? null : SafeCategory(exception)));
            db.AssistantExecutionMetrics.Add(metric);
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (DbUpdateException) { logger.LogWarning("Assistant execution metric could not be persisted. AssistantExecutionId: {AssistantExecutionId}", measurement.ExecutionId); }
        catch (Exception ex) { logger.LogWarning(ex, "Assistant execution metric writer failed. AssistantExecutionId: {AssistantExecutionId}", measurement.ExecutionId); }
    }
    private static string SafeCategory(Exception? exception) => exception switch
    {
        OperationCanceledException => "cancelled", HttpRequestException => "network", TimeoutException => "timeout",
        InvalidOperationException => "provider_error", _ => "assistant_error"
    };
}
