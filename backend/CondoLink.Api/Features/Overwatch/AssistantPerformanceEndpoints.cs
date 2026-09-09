using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch;

public static class AssistantPerformanceEndpoints
{
    public static IEndpointRouteBuilder MapAssistantPerformanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/assistant-performance", SummaryAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        endpoints.MapGet("/overwatch/assistant-performance/executions/{assistantExecutionId:guid}", DetailAsync).RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }
    private static async Task<IResult> SummaryAsync(string? period, Guid? condominiumId, int? take, AppDbContext db, CancellationToken ct)
    {
        var hours = period switch { null or "24h" => 24, "1h" => 1, "7d" => 168, "30d" => 720, _ => 0 };
        if (hours == 0) return Results.BadRequest(new { error = "invalid_period" });
        if (condominiumId is Guid id && !await db.Condominiums.AnyAsync(x => x.Id == id, ct)) return Results.NotFound();
        var since = DateTime.UtcNow.AddHours(-hours);
        var query = db.AssistantExecutionMetrics.AsNoTracking().Where(x => x.StartedAt >= since);
        if (condominiumId is Guid filter) query = query.Where(x => x.CondominiumId == filter);
        var rows = await query.ToArrayAsync(ct);
        object Phase(string name, Func<AssistantExecutionMetric, long?> value) => Aggregate(name, rows.Select(value));
        object Count(string name, Func<AssistantExecutionMetric, int?> value) => Aggregate(name, rows.Select(x => x is null ? (long?)null : value(x)));
        var recent = await query.OrderByDescending(x => x.StartedAt).Take(Math.Clamp(take ?? 50, 1, 100))
            .Join(db.Condominiums.AsNoTracking(), x => x.CondominiumId, c => c.Id, (x, c) => new { x.StartedAt, x.AssistantExecutionId, x.CondominiumId, condominiumName = c.Name, x.Success, x.TotalDurationMs, x.TimeToFirstTokenMs, x.RetrievalDurationMs, x.RerankDurationMs, x.LoadedChunks, x.FinalChunks, x.RerankFallbackUsed, x.ChatModel, x.ErrorCategory }).ToArrayAsync(ct);
        var ai = await db.AiOperationMetrics.AsNoTracking().Where(x => x.Timestamp >= since).GroupBy(x => new { x.Operation, x.Model }).Select(g => new { g.Key.Operation, g.Key.Model, count = g.Count(), successRate = Math.Round(100d * g.Count(x => x.Succeeded) / g.Count(), 2), averageDurationMs = g.Average(x => x.DurationMs), inputTokens = g.Sum(x => x.InputTokens ?? 0), outputTokens = g.Sum(x => x.OutputTokens ?? 0) }).ToArrayAsync(ct);
        return Results.Ok(new { period = period ?? "24h", totalExecutions = rows.Length, successCount = rows.Count(x => x.Success), failureCount = rows.Count(x => !x.Success), successRate = rows.Length == 0 ? (double?)null : Math.Round(100d * rows.Count(x => x.Success) / rows.Length, 2), phases = new[] { Phase("Total", x => x.TotalDurationMs), Phase("TTFT", x => x.TimeToFirstTokenMs), Phase("Retrieval", x => x.RetrievalDurationMs), Phase("Expansion", x => x.ExpansionDurationMs), Phase("Embedding", x => x.EmbeddingDurationMs), Phase("DatabaseMaterialization", x => x.DatabaseMaterializationDurationMs), Phase("EmbeddingDeserialization", x => x.EmbeddingDeserializationDurationMs), Phase("VectorScoring", x => x.VectorScoringDurationMs), Phase("Rerank", x => x.RerankDurationMs), Phase("Chat", x => x.ChatDurationMs), Phase("Generation", x => x.GenerationDurationMs) }.Where(x => ((dynamic)x).count > 0), retrieval = new[] { Count("EligibleChunks", x => x.EligibleChunks), Count("LoadedChunks", x => x.LoadedChunks), Count("DeserializedEmbeddings", x => x.DeserializedEmbeddings), Count("CandidatesBeforeRerank", x => x.CandidatesBeforeRerank), Count("CandidatesAfterRerank", x => x.CandidatesAfterRerank), Count("FinalChunks", x => x.FinalChunks), Count("ContextCharacters", x => x.ContextCharacters) }.Where(x => ((dynamic)x).count > 0), rerank = new { executionsWithRerank = rows.Count(x => x.RerankDurationMs.HasValue), executionsWithRerankFallback = rows.Count(x => x.RerankFallbackUsed), fallbackRate = rows.Count(x => x.RerankDurationMs.HasValue) == 0 ? (double?)null : Math.Round(100d * rows.Count(x => x.RerankFallbackUsed) / rows.Count(x => x.RerankDurationMs.HasValue), 2), fallback = Aggregate("Fallback", rows.Where(x => x.RerankFallbackUsed).Select(x => x.RerankFallbackDurationMs)) }, aiOperations = ai, recent });
    }
    private static async Task<IResult> DetailAsync(Guid assistantExecutionId, AppDbContext db, CancellationToken ct)
    {
        var row = await db.AssistantExecutionMetrics.AsNoTracking().SingleOrDefaultAsync(x => x.AssistantExecutionId == assistantExecutionId, ct);
        return row is null ? Results.NotFound() : Results.Ok(row);
    }
    private static object Aggregate(string name, IEnumerable<long?> source)
    {
        var values = source.Where(x => x.HasValue).Select(x => x!.Value).Order().ToArray();
        long? P(double q) => values.Length == 0 ? null : values[(int)Math.Ceiling(values.Length * q) - 1];
        return new { name, count = values.Length, average = values.Length == 0 ? (double?)null : Math.Round(values.Average(), 2), p50 = P(.5), p95 = P(.95), max = values.Length == 0 ? (long?)null : values[^1] };
    }
}
