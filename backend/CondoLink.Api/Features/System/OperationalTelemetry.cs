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
                var isStreamingResponse = response?.Content?.Headers.ContentType?.MediaType == "text/event-stream";
                if (response?.Content is not null && !isStreamingResponse)
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
    private readonly object gate=new(); private readonly Queue<ApiRequestSample> samples=new();
    public void Record(ApiRequestSample sample){lock(gate){samples.Enqueue(sample);Prune(sample.Timestamp.AddHours(-24));}}
    public long Recent5xx { get { lock(gate){Prune(DateTime.UtcNow.AddHours(-24));return samples.LongCount(x=>x.Timestamp>=DateTime.UtcNow.AddHours(-1)&&x.StatusCode>=500);} } }
    public object Performance(DateTime now)
    {
        lock(gate){Prune(now.AddHours(-24));return new{periods=new[]{Aggregate("1h",samples.Where(x=>x.Timestamp>=now.AddHours(-1))),Aggregate("24h",samples)},topSlowest=samples.GroupBy(x=>new{x.Method,x.Route}).Select(g=>Endpoint(g.Key.Method,g.Key.Route,g)).OrderByDescending(x=>x.P95Ms).ThenByDescending(x=>x.AverageMs).Take(10).ToArray()};}
    }
    public string Health(DateTime now)
    {
        lock(gate)
        {
            Prune(now.AddHours(-24));
            var rows=samples.Where(x=>x.Timestamp>=now.AddHours(-1)&&!IsHeavy(x.Route)).ToArray();
            if(rows.Length<20)return "Healthy";
            var errorRate=100d*rows.Count(x=>x.StatusCode>=500)/rows.Length;
            var p95=P95(rows.Select(x=>x.DurationMs).Order().ToArray());
            return errorRate>5?"Unhealthy":errorRate>=1||p95>1000?"Degraded":"Healthy";
        }
    }
    private void Prune(DateTime cutoff){while(samples.TryPeek(out var x)&&x.Timestamp<cutoff)samples.Dequeue();}
    private static object Aggregate(string period,IEnumerable<ApiRequestSample> source){var a=source.ToArray();var d=a.Select(x=>x.DurationMs).Order().ToArray();var errors=a.Count(x=>x.StatusCode>=500);return new{period,requests=a.Length,averageMs=a.Length==0?0:Math.Round(a.Average(x=>x.DurationMs),1),p95Ms=P95(d),errors5xx=errors,errorRate5xx=a.Length==0?0:Math.Round(100d*errors/a.Length,2),sampleSmall=a.Length<20,averageResponseBytes=a.Where(x=>x.ResponseBytes.HasValue).Select(x=>(double)x.ResponseBytes!.Value).DefaultIfEmpty().Average(),averageQueries=a.Length==0?0:Math.Round(a.Average(x=>x.QueryCount),1),slowQueries=a.Sum(x=>x.SlowQueryCount)};}
    private static EndpointPerformance Endpoint(string method,string route,IEnumerable<ApiRequestSample> source){var a=source.ToArray();var d=a.Select(x=>x.DurationMs).Order().ToArray();return new(method,route,a.Length,Math.Round(a.Average(x=>x.DurationMs),1),P95(d),a.Count(x=>x.StatusCode>=500),Math.Round(a.Average(x=>x.QueryCount),1),a.Max(x=>x.QueryCount),a.Sum(x=>x.SlowQueryCount),a.Where(x=>x.ResponseBytes.HasValue).Select(x=>(double)x.ResponseBytes!.Value).DefaultIfEmpty().Average(),IsHeavy(route),a.Length<20);}
    internal static bool IsHeavy(string route) => route.Contains("setup/confirm",StringComparison.OrdinalIgnoreCase)||route.Contains("import",StringComparison.OrdinalIgnoreCase)||route.Contains("export",StringComparison.OrdinalIgnoreCase)||route.Contains("documents",StringComparison.OrdinalIgnoreCase);
    private static double P95(double[] values)=>values.Length==0?0:Math.Round(values[(int)Math.Ceiling(values.Length*.95)-1],1);
}
public sealed record ApiRequestSample(DateTime Timestamp,string Method,string Route,int StatusCode,double DurationMs,long? ResponseBytes,int QueryCount,int SlowQueryCount,double SqlDurationMs,double MaximumSqlDurationMs);
public sealed record EndpointPerformance(string Method,string Route,int Calls,double AverageMs,double P95Ms,int Errors5xx,double AverageQueries,int MaximumQueries,int SlowQueries,double AverageResponseBytes,bool IsHeavyOperation,bool SampleSmall);

public sealed class OperationalRetentionWorker(IServiceScopeFactory scopes, OperationalTelemetry telemetry, ILogger<OperationalRetentionWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    internal static bool ShouldRemoveHeartbeat(WorkerHeartbeat row, DateTime now)
    {
        var workerWindow = TimeSpan.FromSeconds(Math.Max(1, row.ExpectedIntervalSeconds) * 30L);
        var retention = workerWindow > TimeSpan.FromDays(7) ? workerWindow : TimeSpan.FromDays(7);
        return row.LastHeartbeatAt != default && now - row.LastHeartbeatAt > retention;
    }
    internal static async Task<int> DeleteExpiredHeartbeatsAsync(AppDbContext db,
        DateTime now, CancellationToken ct = default)
    {
        var candidates = await db.WorkerHeartbeats
            .Where(x => x.LastHeartbeatAt < now.AddDays(-7)).ToListAsync(ct);
        var expired = candidates.Where(x => ShouldRemoveHeartbeat(x, now)).ToArray();
        if (expired.Length == 0) return 0;
        db.WorkerHeartbeats.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
        return expired.Length;
    }
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await telemetry.RecordWorkerAsync(nameof(OperationalRetentionWorker), true, Interval, "started", ct: ct); await using var s = scopes.CreateAsyncScope(); var db = s.ServiceProvider.GetRequiredService<AppDbContext>(); var now = DateTime.UtcNow; var cutoff = now.AddDays(-30); var count = await db.AiOperationMetrics.Where(x => x.Timestamp < cutoff).ExecuteDeleteAsync(ct) + await db.OperationalEvents.Where(x => x.Timestamp < cutoff).ExecuteDeleteAsync(ct); count += await DeleteExpiredHeartbeatsAsync(db, now, ct); await telemetry.RecordWorkerAsync(nameof(OperationalRetentionWorker), true, Interval, "completed", true, count, ct: ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Operational retention failed."); await telemetry.EventAsync("Workers", "Retention", "Error", "retention_failed", ct: CancellationToken.None); }
            await Task.Delay(Interval, ct);
        }
    }
}
