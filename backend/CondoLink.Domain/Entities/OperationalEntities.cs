namespace CondoLink.Domain.Entities;

public sealed class WorkerHeartbeat
{
    private WorkerHeartbeat() { }
    public WorkerHeartbeat(string workerName, string instanceId, bool enabled, int expectedIntervalSeconds)
    { WorkerName = workerName; InstanceId = instanceId; Enabled = enabled; ExpectedIntervalSeconds = expectedIntervalSeconds; }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string WorkerName { get; private set; } = null!;
    public string InstanceId { get; private set; } = null!;
    public bool Enabled { get; private set; }
    public int ExpectedIntervalSeconds { get; private set; }
    public DateTime LastHeartbeatAt { get; private set; }
    public DateTime? LastStartedAt { get; private set; }
    public DateTime? LastCompletedAt { get; private set; }
    public bool? LastSucceeded { get; private set; }
    public int? LastProcessedItems { get; private set; }
    public int? LastFailureCount { get; private set; }
    public string? LastResultCode { get; private set; }
    public void Beat(DateTime now, bool enabled, int interval) { LastHeartbeatAt = now; Enabled = enabled; ExpectedIntervalSeconds = interval; }
    public void Started(DateTime now) { LastHeartbeatAt = now; LastStartedAt = now; }
    public void Completed(DateTime now, bool succeeded, int? items, int failures, string? code)
    { LastHeartbeatAt = now; LastCompletedAt = now; LastSucceeded = succeeded; LastProcessedItems = items; LastFailureCount = failures; LastResultCode = code?[..Math.Min(100, code.Length)]; }
}

public sealed class AiOperationMetric
{
    private AiOperationMetric() { }
    public AiOperationMetric(string operation, string? model, DateTime timestamp, long durationMs, bool succeeded, int? inputTokens, int? outputTokens, int? totalTokens, string? errorCategory)
    { Operation = operation; Model = model; Timestamp = timestamp; DurationMs = durationMs; Succeeded = succeeded; InputTokens = inputTokens; OutputTokens = outputTokens; TotalTokens = totalTokens ?? (inputTokens is null && outputTokens is null ? null : inputTokens.GetValueOrDefault() + outputTokens.GetValueOrDefault()); ErrorCategory = errorCategory; }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Operation { get; private set; } = null!;
    public string? Model { get; private set; }
    public DateTime Timestamp { get; private set; }
    public long DurationMs { get; private set; }
    public bool Succeeded { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public int? TotalTokens { get; private set; }
    public string? ErrorCategory { get; private set; }
}

/// <summary>Sanitized, one-row operational view of a complete assistant request.</summary>
public sealed class AssistantExecutionMetric
{
    private AssistantExecutionMetric() { }
    public AssistantExecutionMetric(Guid assistantExecutionId, Guid condominiumId, DateTime startedAt)
    { AssistantExecutionId = assistantExecutionId; CondominiumId = condominiumId; StartedAt = startedAt; }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid AssistantExecutionId { get; private set; }
    public Guid CondominiumId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public bool Success { get; private set; }
    public long? TotalDurationMs { get; private set; }
    public long? RetrievalDurationMs { get; private set; }
    public long? ExpansionDurationMs { get; private set; }
    public long? EmbeddingDurationMs { get; private set; }
    public long? DatabaseMaterializationDurationMs { get; private set; }
    public long? EmbeddingDeserializationDurationMs { get; private set; }
    public long? VectorScoringDurationMs { get; private set; }
    public long? LexicalScoringDurationMs { get; private set; }
    public long? RerankDurationMs { get; private set; }
    public long? RerankFallbackDurationMs { get; private set; }
    public long? ContextPreparationDurationMs { get; private set; }
    public long? ChatDurationMs { get; private set; }
    public long? TimeToFirstTokenMs { get; private set; }
    public long? GenerationDurationMs { get; private set; }
    public int? EligibleDocuments { get; private set; }
    public int? EligibleChunks { get; private set; }
    public int? LoadedChunks { get; private set; }
    public int? DeserializedEmbeddings { get; private set; }
    public int? ExpandedQueries { get; private set; }
    public int? CandidatesBeforeRerank { get; private set; }
    public int? CandidatesAfterRerank { get; private set; }
    public int? FinalChunks { get; private set; }
    public int? ContextCharacters { get; private set; }
    public int? ContextTokensApprox { get; private set; }
    public string? EmbeddingModel { get; private set; }
    public string? ChatModel { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public int? RetryCount { get; private set; }
    public string? RerankModel { get; private set; }
    public bool RerankAttempted { get; private set; }
    public bool RerankSucceeded { get; private set; }
    public bool RerankTimedOut { get; private set; }
    public bool RerankFastPathUsed { get; private set; }
    public int? RerankCandidatesSent { get; private set; }
    public int? RerankPayloadBytes { get; private set; }
    public int? RerankInputTokensApprox { get; private set; }
    public bool RerankFallbackUsed { get; private set; }
    public string? ErrorCategory { get; private set; }
    public string? ErrorCode { get; private set; }
    public void Complete(DateTime completedAt, bool success, AssistantExecutionMetricValues values)
    {
        CompletedAt = completedAt; Success = success;
        TotalDurationMs = values.TotalDurationMs; RetrievalDurationMs = values.RetrievalDurationMs;
        ExpansionDurationMs = values.ExpansionDurationMs; EmbeddingDurationMs = values.EmbeddingDurationMs;
        DatabaseMaterializationDurationMs = values.DatabaseMaterializationDurationMs;
        EmbeddingDeserializationDurationMs = values.EmbeddingDeserializationDurationMs;
        VectorScoringDurationMs = values.VectorScoringDurationMs; LexicalScoringDurationMs = values.LexicalScoringDurationMs;
        RerankDurationMs = values.RerankDurationMs; RerankFallbackDurationMs = values.RerankFallbackDurationMs;
        ContextPreparationDurationMs = values.ContextPreparationDurationMs; ChatDurationMs = values.ChatDurationMs;
        TimeToFirstTokenMs = values.TimeToFirstTokenMs; GenerationDurationMs = values.GenerationDurationMs;
        EligibleDocuments = values.EligibleDocuments; EligibleChunks = values.EligibleChunks; LoadedChunks = values.LoadedChunks;
        DeserializedEmbeddings = values.DeserializedEmbeddings; ExpandedQueries = values.ExpandedQueries;
        CandidatesBeforeRerank = values.CandidatesBeforeRerank; CandidatesAfterRerank = values.CandidatesAfterRerank;
        FinalChunks = values.FinalChunks; ContextCharacters = values.ContextCharacters; ContextTokensApprox = values.ContextTokensApprox;
        EmbeddingModel = values.EmbeddingModel; ChatModel = values.ChatModel; InputTokens = values.InputTokens;
        OutputTokens = values.OutputTokens; RetryCount = values.RetryCount; RerankModel = values.RerankModel;
        RerankAttempted = values.RerankAttempted; RerankSucceeded = values.RerankSucceeded;
        RerankTimedOut = values.RerankTimedOut; RerankFastPathUsed = values.RerankFastPathUsed;
        RerankCandidatesSent = values.RerankCandidatesSent; RerankPayloadBytes = values.RerankPayloadBytes;
        RerankInputTokensApprox = values.RerankInputTokensApprox; RerankFallbackUsed = values.RerankFallbackUsed;
        ErrorCategory = values.ErrorCategory; ErrorCode = values.ErrorCode;
    }
}

public sealed class AssistantExecutionMetricValues
{
    public long? TotalDurationMs { get; init; } public long? RetrievalDurationMs { get; init; } public long? ExpansionDurationMs { get; init; } public long? EmbeddingDurationMs { get; init; } public long? DatabaseMaterializationDurationMs { get; init; } public long? EmbeddingDeserializationDurationMs { get; init; } public long? VectorScoringDurationMs { get; init; } public long? LexicalScoringDurationMs { get; init; } public long? RerankDurationMs { get; init; } public long? RerankFallbackDurationMs { get; init; } public long? ContextPreparationDurationMs { get; init; } public long? ChatDurationMs { get; init; } public long? TimeToFirstTokenMs { get; init; } public long? GenerationDurationMs { get; init; }
    public int? EligibleDocuments { get; init; } public int? EligibleChunks { get; init; } public int? LoadedChunks { get; init; } public int? DeserializedEmbeddings { get; init; } public int? ExpandedQueries { get; init; } public int? CandidatesBeforeRerank { get; init; } public int? CandidatesAfterRerank { get; init; } public int? FinalChunks { get; init; } public int? ContextCharacters { get; init; } public int? ContextTokensApprox { get; init; }
    public string? EmbeddingModel { get; init; } public string? ChatModel { get; init; } public int? InputTokens { get; init; } public int? OutputTokens { get; init; } public int? RetryCount { get; init; }
    public string? RerankModel { get; init; } public bool RerankAttempted { get; init; } public bool RerankSucceeded { get; init; } public bool RerankTimedOut { get; init; } public bool RerankFastPathUsed { get; init; } public int? RerankCandidatesSent { get; init; } public int? RerankPayloadBytes { get; init; } public int? RerankInputTokensApprox { get; init; }
    public bool RerankFallbackUsed { get; init; } public string? ErrorCategory { get; init; } public string? ErrorCode { get; init; }
}

public sealed class OperationalEvent
{
    private OperationalEvent() { }
    public OperationalEvent(DateTime timestamp, string component, string category, string severity, string reasonCode, string? correlationId = null)
    { Timestamp = timestamp; Component = component; Category = category; Severity = severity; ReasonCode = reasonCode; CorrelationId = correlationId; }
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateTime Timestamp { get; private set; }
    public string Component { get; private set; } = null!;
    public string Category { get; private set; } = null!;
    public string Severity { get; private set; } = null!;
    public string ReasonCode { get; private set; } = null!;
    public string? CorrelationId { get; private set; }
}
