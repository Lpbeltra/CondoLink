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
