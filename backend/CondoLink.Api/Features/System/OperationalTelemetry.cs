using System.Diagnostics;
using System.Text.Json;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Observability;

public sealed class OperationalTelemetry(IServiceScopeFactory scopes, TimeProvider clock)
{
    public static string InstanceId { get; } = $"{Environment.MachineName}-{Environment.ProcessId}";

    public async Task RecordWorkerAsync(string name, bool enabled, TimeSpan interval, string phase,
        bool? succeeded = null, int? items = null, int failures = 0, string? code = null, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.WorkerHeartbeats.SingleOrDefaultAsync(x => x.WorkerName == name && x.InstanceId == InstanceId, ct);
        row ??= new WorkerHeartbeat(name, InstanceId, enabled, Math.Max(1, (int)interval.TotalSeconds));
        if (db.Entry(row).State == EntityState.Detached) db.WorkerHeartbeats.Add(row);
        var now = clock.GetUtcNow().UtcDateTime;
        row.Beat(now, enabled, Math.Max(1, (int)interval.TotalSeconds));
        if (phase == "started") row.Started(now);
        if (phase == "completed") row.Completed(now, succeeded == true, items, failures, code);
        await db.SaveChangesAsync(ct);
    }

    public async Task EventAsync(string component, string category, string severity, string reason, string? correlation = null, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.OperationalEvents.Add(new OperationalEvent(clock.GetUtcNow().UtcDateTime, component, category, severity, SafeCode(reason), correlation));
        await db.SaveChangesAsync(ct);
    }

    internal static string SafeCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "unknown";
        var trimmed = value.Trim();
        return trimmed.Length <= 100 && trimmed.All(c => char.IsLetterOrDigit(c) || c is '_' or '-' or '.')
            ? trimmed : "invalid_reason_code";
    }
}

public sealed class OpenAiTelemetryHandler(IServiceScopeFactory scopes, TimeProvider clock, string operation) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var started = Stopwatch.GetTimestamp(); HttpResponseMessage? response = null; string? error = null;
        try { response = await base.SendAsync(request, ct); if (!response.IsSuccessStatusCode) error = $"http_{(int)response.StatusCode}"; return response; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { error = "timeout"; throw; }
        catch (HttpRequestException) { error = "network"; throw; }
        finally
        {
            try
            {
                string? model = null; int? input = null; int? output = null; int? total = null;
                if (response?.Content is not null)
                {
                    var json = await response.Content.ReadAsStringAsync(CancellationToken.None);
                    try { if (!string.IsNullOrWhiteSpace(json)) using (var doc = JsonDocument.Parse(json))
                    {
                        var root = doc.RootElement;
                        if (root.TryGetProperty("model", out var m)) model = m.GetString();
                        if (root.TryGetProperty("usage", out var usage))
                        { input = Token(usage, "prompt_tokens") ?? Token(usage, "input_tokens"); output = Token(usage, "completion_tokens") ?? Token(usage, "output_tokens"); total = Token(usage, "total_tokens"); }
                    } } catch (JsonException) { }
                }
                await using var scope = scopes.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.AiOperationMetrics.Add(new AiOperationMetric(operation, model, clock.GetUtcNow().UtcDateTime,
                    (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds, error is null && response?.IsSuccessStatusCode == true, input, output, total, error));
                if (error is not null) db.OperationalEvents.Add(new OperationalEvent(clock.GetUtcNow().UtcDateTime, "OpenAI", operation, "Error", error));
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch { /* telemetry must never break the product path */ }
        }
    }
    private static int? Token(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.TryGetInt32(out var n) ? n : null;
}

public sealed class ApiRequestMetrics
{
    private long _recent5xx; private DateTime _window = DateTime.UtcNow;
    public void Record(int status) { if (DateTime.UtcNow - _window > TimeSpan.FromHours(1)) { Interlocked.Exchange(ref _recent5xx, 0); _window = DateTime.UtcNow; } if (status >= 500) Interlocked.Increment(ref _recent5xx); }
    public long Recent5xx => Interlocked.Read(ref _recent5xx);
}

public sealed class OperationalRetentionWorker(IServiceScopeFactory scopes, OperationalTelemetry telemetry, ILogger<OperationalRetentionWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await telemetry.RecordWorkerAsync(nameof(OperationalRetentionWorker), true, Interval, "started", ct: ct); await using var s = scopes.CreateAsyncScope(); var db = s.ServiceProvider.GetRequiredService<AppDbContext>(); var cutoff = DateTime.UtcNow.AddDays(-30); var count = await db.AiOperationMetrics.Where(x => x.Timestamp < cutoff).ExecuteDeleteAsync(ct) + await db.OperationalEvents.Where(x => x.Timestamp < cutoff).ExecuteDeleteAsync(ct); await telemetry.RecordWorkerAsync(nameof(OperationalRetentionWorker), true, Interval, "completed", true, count, ct: ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Operational retention failed."); await telemetry.EventAsync("Workers", "Retention", "Error", "retention_failed", ct: CancellationToken.None); }
            await Task.Delay(Interval, ct);
        }
    }
}
