using System.Diagnostics;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.Overwatch;

public static class GetSystemStatus
{
    public static IEndpointRouteBuilder MapGetSystemStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/system", HandleAsync).RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch").WithSummary("Get operational system status");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(AppDbContext db, IOptions<WhatsAppOptions> waOptions,
        IOptions<EmailOptions> emailOptions, IOptions<RequestDraftAiOptions> aiOptions,
        ApiRequestMetrics apiMetrics, IWebHostEnvironment environment, CancellationToken ct)
    {
        var now = DateTime.UtcNow; var sw = Stopwatch.StartNew();
        try { await db.Database.ExecuteSqlRawAsync("SELECT 1", ct); }
        catch
        {
            return Results.Ok(new { generatedAt = now, globalStatus = "Unhealthy", components = new[] {
                Component("API", "Healthy", "Processo respondendo"), Component("PostgreSQL", "Unhealthy", "Conexão indisponível"),
                Component("WhatsApp", "Unknown", "Dados indisponíveis"), Component("OpenAI", "Unknown", "Dados indisponíveis"),
                Component("E-mail", "Unknown", "Dados indisponíveis"), Component("Workers", "Unknown", "Dados indisponíveis") }, databaseLatencyMs = (long?)null });
        }
        sw.Stop();
        var workers = await db.WorkerHeartbeats.AsNoTracking().OrderBy(x => x.WorkerName).ThenBy(x => x.InstanceId).ToListAsync(ct);
        var workerDtos = workers.Select(x => new { x.WorkerName, x.InstanceId, status = WorkerStatus(x, now), x.Enabled, x.ExpectedIntervalSeconds, x.LastHeartbeatAt, x.LastStartedAt, x.LastCompletedAt, x.LastSucceeded, x.LastProcessedItems, x.LastFailureCount, x.LastResultCode }).ToArray();
        var wa = waOptions.Value; var queueStatuses = new[] { WhatsAppOutboundStatus.Pending, WhatsAppOutboundStatus.Processing };
        var outbound = await db.WhatsAppOutboundMessages.AsNoTracking().GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Status, x => x.Count, ct);
        var oldest = await db.WhatsAppOutboundMessages.AsNoTracking().Where(x => queueStatuses.Contains(x.Status)).MinAsync(x => (DateTime?)x.CreatedAt, ct);
        var last24 = now.AddHours(-24); var sent24 = await db.WhatsAppOutboundMessages.CountAsync(x => x.SentAt >= last24, ct);
        var failed24 = await db.WhatsAppOutboundMessages.CountAsync(x => x.FailedAt >= last24, ct);
        var inbound24 = await db.WhatsAppInboundMessages.CountAsync(x => x.ReceivedAt >= last24, ct);
        var lastWebhook = await db.WhatsAppInboundMessages.MaxAsync(x => (DateTime?)x.ReceivedAt, ct);
        var waWorker = workers.Where(x => x.WorkerName == nameof(WhatsAppOutboundWorker)).OrderByDescending(x => x.LastHeartbeatAt).FirstOrDefault();
        var waStatus = !wa.Enabled ? "Disabled" : !wa.OutboundWorkerEnabled ? "Degraded" : waWorker is null || WorkerStatus(waWorker, now) == "Unhealthy" ? "Unhealthy" : oldest is not null && now - oldest > TimeSpan.FromMinutes(5) || failed24 >= 5 ? "Degraded" : "Healthy";
        var periods = new[] { (Name: "1h", Since: now.AddHours(-1)), (Name: "24h", Since: last24), (Name: "7d", Since: now.AddDays(-7)) };
        var aiRows = await db.AiOperationMetrics.AsNoTracking().Where(x => x.Timestamp >= now.AddDays(-7)).ToListAsync(ct);
        object Aggregate(IEnumerable<CondoLink.Domain.Entities.AiOperationMetric> source) { var a = source.ToArray(); var durations = a.Select(x => x.DurationMs).Order().ToArray(); return new { calls = a.Length, failures = a.Count(x => !x.Succeeded), successRate = a.Length == 0 ? (double?)null : Math.Round(100d * a.Count(x => x.Succeeded) / a.Length, 1), averageLatencyMs = a.Length == 0 ? (double?)null : Math.Round(a.Average(x => x.DurationMs), 0), p95LatencyMs = durations.Length == 0 ? (long?)null : durations[(int)Math.Ceiling(durations.Length * .95) - 1], inputTokens = a.Sum(x => x.InputTokens ?? 0), outputTokens = a.Sum(x => x.OutputTokens ?? 0), totalTokens = a.Sum(x => x.TotalTokens ?? 0) }; }
        var aiPeriods = periods.Select(p => new { period = p.Name, metrics = Aggregate(aiRows.Where(x => x.Timestamp >= p.Since)) }).ToArray();
        var aiBreakdown = aiRows.GroupBy(x => new { x.Operation, x.Model }).Select(g => new { g.Key.Operation, g.Key.Model, metrics = Aggregate(g) }).OrderByDescending(x => x.Operation).ToArray();
        var aiConfigured = !string.IsNullOrWhiteSpace(aiOptions.Value.ApiKey); var recentAi = aiRows.Where(x => x.Timestamp >= last24).ToArray();
        var aiStatus = !aiConfigured ? "Unhealthy" : recentAi.Length == 0 ? "Unknown" : recentAi.Count(x => !x.Succeeded) * 5 > recentAi.Length ? "Degraded" : "Healthy";
        var email = emailOptions.Value; var emailConfigured = !string.IsNullOrWhiteSpace(email.Host) && !string.IsNullOrWhiteSpace(email.FromAddress);
        var recentEvents = await db.OperationalEvents.AsNoTracking().Where(x => x.Timestamp >= now.AddDays(-30)).OrderByDescending(x => x.Timestamp).Take(50).Select(x => new { x.Timestamp, x.Component, x.Category, x.Severity, x.ReasonCode, x.CorrelationId }).ToArrayAsync(ct);
        var emailEvents = recentEvents.Where(x => x.Component == "Email").ToArray(); var emailStatus = !email.Enabled ? "Disabled" : !emailConfigured ? "Unhealthy" : emailEvents.Any(x => x.Severity == "Error" && x.Timestamp >= last24) ? "Degraded" : "Healthy";
        var workersStatus = workerDtos.Any(x => x.status == "Unhealthy") ? "Unhealthy" : workerDtos.Length == 0 || workerDtos.Any(x => x.status is "Degraded" or "Unknown") ? "Degraded" : "Healthy";
        var components = new[] { Component("API", "Healthy", $"Uptime {Environment.TickCount64 / 1000}s; 5xx/1h: {apiMetrics.Recent5xx}"), Component("PostgreSQL", "Healthy", $"Check em {sw.ElapsedMilliseconds}ms"), Component("WhatsApp", waStatus, wa.Enabled ? "Integração habilitada" : "Desabilitado por configuração"), Component("OpenAI", aiStatus, recentAi.Length == 0 ? "Sem atividade recente" : $"{recentAi.Length} chamadas/24h"), Component("E-mail", emailStatus, email.Enabled ? (emailConfigured ? "Configuração presente" : "Configuração incompleta") : "Desabilitado por configuração"), Component("Workers", workersStatus, $"{workerDtos.Length} instâncias registradas") };
        var global = components.Any(x => x.status == "Unhealthy") ? "Unhealthy" : components.Any(x => x.status is "Degraded" or "Unknown") ? "Degraded" : "Healthy";
        var requests24 = await db.Requests.CountAsync(x => x.CreatedAt >= last24, ct);
        return Results.Ok(new { generatedAt = now, globalStatus = global, api = new { status = "Healthy", uptimeSeconds = Environment.TickCount64 / 1000, environment = environment.EnvironmentName, version = typeof(GetSystemStatus).Assembly.GetName().Version?.ToString(), serverTime = now, recent5xx = apiMetrics.Recent5xx }, performance = apiMetrics.Performance(now), database = new { status = "Healthy", latencyMs = sw.ElapsedMilliseconds }, components, activity24h = new { requestsCreated = requests24, whatsappReceived = inbound24, whatsappSent = sent24, aiCalls = recentAi.Length, operationalErrors = recentEvents.Count(x => x.Severity == "Error" && x.Timestamp >= last24) }, workers = workerDtos, whatsapp = new { status = waStatus, enabled = wa.Enabled, outboundWorkerEnabled = wa.OutboundWorkerEnabled, queued = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Pending), sending = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Processing), waiting = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Pending), failed = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Failed) + outbound.GetValueOrDefault(WhatsAppOutboundStatus.PermanentlyFailed), delivered = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Delivered), read = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Read), sent24h = sent24, failed24h = failed24, oldestQueuedAt = oldest, oldestQueuedAgeSeconds = oldest is null ? (long?)null : (long)(now - oldest.Value).TotalSeconds, lastOutboundProcessing = waWorker?.LastCompletedAt, lastWebhookReceived = lastWebhook }, ai = new { status = aiStatus, configured = aiConfigured, periods = aiPeriods, breakdown = aiBreakdown }, email = new { status = emailStatus, enabled = email.Enabled, configured = emailConfigured, lastSend = emailEvents.FirstOrDefault()?.Timestamp, failures24h = emailEvents.Count(x => x.Severity == "Error" && x.Timestamp >= last24), successes24h = emailEvents.Count(x => x.Severity == "Info" && x.Timestamp >= last24) }, recentEvents });
    }
    private static ComponentDto Component(string name, string status, string detail) => new(name, status, detail);
    private sealed record ComponentDto(string name, string status, string detail);
    internal static string WorkerStatus(CondoLink.Domain.Entities.WorkerHeartbeat x, DateTime now) => !x.Enabled ? "Disabled" : x.LastHeartbeatAt == default ? "Unknown" : now - x.LastHeartbeatAt > TimeSpan.FromSeconds(x.ExpectedIntervalSeconds * 5) ? "Unhealthy" : now - x.LastHeartbeatAt > TimeSpan.FromSeconds(x.ExpectedIntervalSeconds * 2.5) || x.LastSucceeded == false ? "Degraded" : "Healthy";
}
