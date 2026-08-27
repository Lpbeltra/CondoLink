using System.Diagnostics;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.Requests;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.Overwatch;

public static class GetSystemStatus
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static IEndpointRouteBuilder MapGetSystemStatus(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/overwatch/system", HandleAsync).RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch").WithSummary("Get operational system status");
        endpoints.MapGet("/overwatch/system/diagnostic", DiagnosticAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch")
            .WithSummary("Export a sanitized operational diagnostic");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(AppDbContext db, IOptions<WhatsAppOptions> waOptions,
        IOptions<EmailOptions> emailOptions, IOptions<RequestDraftAiOptions> aiOptions,
        ApiRequestMetrics apiMetrics, IWebHostEnvironment environment, CancellationToken ct)
        => Results.Json(await BuildAsync(db, waOptions, emailOptions, aiOptions,
            apiMetrics, environment, ct));

    internal static async Task<JsonElement> BuildAsync(AppDbContext db, IOptions<WhatsAppOptions> waOptions,
        IOptions<EmailOptions> emailOptions, IOptions<RequestDraftAiOptions> aiOptions,
        ApiRequestMetrics apiMetrics, IWebHostEnvironment environment, CancellationToken ct)
    {
        var now = DateTime.UtcNow; var sw = Stopwatch.StartNew();
        try { await db.Database.ExecuteSqlRawAsync("SELECT 1", ct); }
        catch
        {
            return Element(new { generatedAt = now, globalStatus = "Unhealthy", components = new[] {
                Component("API", "Healthy", "Processo respondendo"), Component("PostgreSQL", "Unhealthy", "Conexão indisponível"),
                Component("WhatsApp", "Unknown", "Dados indisponíveis"), Component("OpenAI", "Unknown", "Dados indisponíveis"),
                Component("E-mail", "Unknown", "Dados indisponíveis"), Component("Workers", "Unknown", "Dados indisponíveis") }, databaseLatencyMs = (long?)null });
        }
        sw.Stop();
        var storedWorkers = await db.WorkerHeartbeats.AsNoTracking().OrderBy(x => x.WorkerName).ThenBy(x => x.InstanceId).ToListAsync(ct);
        var workers = storedWorkers.Where(x => IsActiveInstance(x, now)).ToList();
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
        var aiEnabled = aiOptions.Value.Enabled;
        var aiStatus = !aiEnabled ? "Disabled" : !aiConfigured ? "Unhealthy" : recentAi.Length == 0 ? "Unknown" : recentAi.Count(x => !x.Succeeded) * 5 > recentAi.Length ? "Degraded" : "Healthy";
        var email = emailOptions.Value; var emailConfigured = !string.IsNullOrWhiteSpace(email.Host) && !string.IsNullOrWhiteSpace(email.FromAddress);
        var recentEvents = await db.OperationalEvents.AsNoTracking().Where(x => x.Timestamp >= now.AddDays(-30)).OrderByDescending(x => x.Timestamp).Take(100).Select(x => new { x.Timestamp, x.Component, x.Category, x.Severity, x.ReasonCode, x.CorrelationId }).ToArrayAsync(ct);
        var emailEvents = recentEvents.Where(x => x.Component == "Email").ToArray();
        var emailSuccesses = emailEvents.Count(x => x.Severity == "Info" && x.Timestamp >= last24);
        var emailFailures = emailEvents.Count(x => x.Severity == "Error" && x.Timestamp >= last24);
        var emailStatus = EmailStatus(email.Enabled, emailConfigured, emailSuccesses, emailFailures);
        var requiredWorkerNames = new[] { nameof(WhatsAppOutboundWorker), nameof(WhatsAppConversationInactivityWorker),
            nameof(RequestClosureWorker), nameof(OperationalRetentionWorker) };
        var workerGroups = requiredWorkerNames.Concat(workerDtos.Select(x => x.WorkerName))
            .Distinct().Select(name =>
            {
                var group = workerDtos.Where(x => x.WorkerName == name).ToArray();
                return group.Length == 0 ? "Degraded"
                    : group.Any(x => x.status == "Healthy") ? "Healthy"
                    : group.Any(x => x.status == "Unhealthy") ? "Unhealthy"
                    : group.Any(x => x.status is "Degraded" or "Unknown") ? "Degraded"
                    : "Disabled";
            }).ToArray();
        var workersStatus = workerGroups.Any(x => x == "Unhealthy") ? "Unhealthy" : workerGroups.Any(x => x == "Degraded") ? "Degraded" : "Healthy";
        var pendingOutbound = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Pending);
        var oldestAge = oldest is null ? "sem fila" : $"mais antiga {(long)(now - oldest.Value).TotalMinutes} min";
        var aiFailures = recentAi.Count(x => !x.Succeeded);
        var apiStatus = apiMetrics.Health(now);
        var components = new[] { Component("API", apiStatus, $"Uptime {Environment.TickCount64 / 1000}s; 5xx/1h: {apiMetrics.Recent5xx}"), Component("PostgreSQL", "Healthy", $"Check em {sw.ElapsedMilliseconds}ms"), Component("WhatsApp", waStatus, wa.Enabled ? $"{pendingOutbound} na fila; {sent24} enviadas; {failed24} falhas/24h; {oldestAge}" : "Desabilitado por configuração"), Component("OpenAI", aiStatus, !aiEnabled ? "Desabilitado por configuração" : recentAi.Length == 0 ? "Sem atividade recente" : $"{recentAi.Length} chamadas; {aiFailures} falhas/24h"), Component("E-mail", emailStatus, email.Enabled ? $"{emailSuccesses} envios; {emailFailures} falhas/24h" : "Desabilitado por configuração"), Component("Workers", workersStatus, $"{workerDtos.Count(x => x.status == "Healthy")} ativos de {requiredWorkerNames.Length} esperados") };
        var global = GlobalStatus(components);
        var requests24 = await db.Requests.CountAsync(x => x.CreatedAt >= last24, ct);
        return Element(new { generatedAt = now, globalStatus = global, api = new { status = "Healthy", uptimeSeconds = Environment.TickCount64 / 1000, environment = environment.EnvironmentName, version = typeof(GetSystemStatus).Assembly.GetName().Version?.ToString(), serverTime = now, recent5xx = apiMetrics.Recent5xx }, performance = apiMetrics.Performance(now), database = new { status = "Healthy", latencyMs = sw.ElapsedMilliseconds }, components, activity24h = new { requestsCreated = requests24, whatsappReceived = inbound24, whatsappSent = sent24, aiCalls = recentAi.Length, operationalErrors = recentEvents.Count(x => x.Severity == "Error" && x.Timestamp >= last24) }, workers = workerDtos, whatsapp = new { status = waStatus, enabled = wa.Enabled, outboundWorkerEnabled = wa.OutboundWorkerEnabled, queued = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Pending), sending = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Processing), waiting = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Pending), failed = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Failed) + outbound.GetValueOrDefault(WhatsAppOutboundStatus.PermanentlyFailed), delivered = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Delivered), read = outbound.GetValueOrDefault(WhatsAppOutboundStatus.Read), sent24h = sent24, failed24h = failed24, oldestQueuedAt = oldest, oldestQueuedAgeSeconds = oldest is null ? (long?)null : (long)(now - oldest.Value).TotalSeconds, lastOutboundProcessing = waWorker?.LastCompletedAt, lastWebhookReceived = lastWebhook }, ai = new { status = aiStatus, enabled = aiEnabled, configured = aiConfigured, periods = aiPeriods, breakdown = aiBreakdown }, email = new { status = emailStatus, enabled = email.Enabled, configured = emailConfigured, lastSend = emailEvents.FirstOrDefault()?.Timestamp, failures24h = emailEvents.Count(x => x.Severity == "Error" && x.Timestamp >= last24), successes24h = emailEvents.Count(x => x.Severity == "Info" && x.Timestamp >= last24) }, recentEvents });
    }

    private static async Task<IResult> DiagnosticAsync(AppDbContext db,
        IOptions<WhatsAppOptions> waOptions, IOptions<EmailOptions> emailOptions,
        IOptions<RequestDraftAiOptions> aiOptions, ApiRequestMetrics apiMetrics,
        IWebHostEnvironment environment, CancellationToken ct)
    {
        var status = await BuildAsync(db, waOptions, emailOptions, aiOptions,
            apiMetrics, environment, ct);
        var generated = status.GetProperty("generatedAt").GetDateTime();
        var text = DiagnosticText(status);
        var filename = $"comvy-diagnostico-{generated:yyyy-MM-dd-HHmmss}.txt";
        return Results.File(Encoding.UTF8.GetBytes(text),
            "text/plain; charset=utf-8", filename);
    }

    internal static string DiagnosticText(JsonElement root)
    {
        var b = new StringBuilder();
        void Section(string title) => b.AppendLine().AppendLine(new string('=', 32))
            .AppendLine(title).AppendLine(new string('=', 32));
        void Field(string name, JsonElement parent, string property) =>
            b.Append(name).Append(": ").AppendLine(Value(parent, property));
        b.AppendLine("COMVY — DIAGNÓSTICO OPERACIONAL");
        Field("Gerado em", root, "generatedAt");
        Section("STATUS GERAL"); Field("Status", root, "globalStatus");
        if (!root.TryGetProperty("api", out var api))
        {
            b.AppendLine("Dados detalhados indisponíveis: PostgreSQL não respondeu ao health check.");
            return b.ToString();
        }
        Field("Ambiente", api, "environment"); Field("Versão", api, "version");
        b.AppendLine().AppendLine("API"); Field("Status", api, "status");
        Field("UptimeSeconds", api, "uptimeSeconds"); Field("5xx 1h", api, "recent5xx");
        var performance = root.GetProperty("performance");
        foreach (var period in performance.GetProperty("periods").EnumerateArray())
        { b.AppendLine($"Requests {Value(period,"period")}: {Value(period,"requests")}; Média: {Value(period,"averageMs")} ms; P95: {Value(period,"p95Ms")} ms; 5xx: {Value(period,"errors5xx")}"); }
        var database = root.GetProperty("database"); b.AppendLine().AppendLine("PostgreSQL");
        Field("Status", database, "status"); Field("HealthCheckMs", database, "latencyMs");
        var wa = root.GetProperty("whatsapp"); b.AppendLine().AppendLine("WhatsApp");
        foreach (var p in new[]{"status","queued","sending","failed24h","delivered","read","lastWebhookReceived"}) Field(p,wa,p);
        var ai = root.GetProperty("ai"); b.AppendLine().AppendLine("OpenAI"); Field("Status",ai,"status");
        foreach(var p in ai.GetProperty("periods").EnumerateArray()) { var m=p.GetProperty("metrics"); b.AppendLine($"{Value(p,"period")}: Calls={Value(m,"calls")}; Success={Value(m,"successRate")}; AverageMs={Value(m,"averageLatencyMs")}; P95Ms={Value(m,"p95LatencyMs")}; Tokens={Value(m,"totalTokens")}"); }
        var email=root.GetProperty("email"); b.AppendLine().AppendLine("E-mail"); foreach(var p in new[]{"status","successes24h","failures24h","lastSend"}) Field(p,email,p);
        Section("WORKERS");
        foreach(var worker in root.GetProperty("workers").EnumerateArray())
        { foreach(var p in new[]{"workerName","instanceId","status","lastHeartbeatAt","lastStartedAt","lastCompletedAt","lastSucceeded","lastProcessedItems","lastFailureCount","lastResultCode"}) Field(p,worker,p); b.AppendLine(); }
        Section("PERFORMANCE DA API"); b.AppendLine("Top endpoints por P95:");
        foreach(var endpoint in performance.GetProperty("topSlowest").EnumerateArray())
        { b.AppendLine($"{Value(endpoint,"method")} {Safe(Value(endpoint,"route"))}"); foreach(var p in new[]{"calls","averageMs","p95Ms","errors5xx","averageQueries","maximumQueries","slowQueries","averageResponseBytes"}) Field(p,endpoint,p); b.AppendLine(); }
        Section("OPENAI POR OPERAÇÃO");
        foreach(var item in ai.GetProperty("breakdown").EnumerateArray()) { Field("Operation",item,"operation"); Field("Model",item,"model"); var m=item.GetProperty("metrics"); foreach(var p in new[]{"calls","successRate","averageLatencyMs","p95LatencyMs","totalTokens"}) Field(p,m,p); b.AppendLine(); }
        Section("EVENTOS OPERACIONAIS RECENTES");
        foreach(var item in root.GetProperty("recentEvents").EnumerateArray()) { foreach(var p in new[]{"timestamp","component","category","severity","reasonCode","correlationId"}) Field(p,item,p); b.AppendLine(); }
        Section("CONFIGURAÇÃO OPERACIONAL");
        b.AppendLine($"WhatsAppEnabled: {Value(wa,"enabled")}")
            .AppendLine($"WhatsAppWorkerEnabled: {Value(wa,"outboundWorkerEnabled")}")
            .AppendLine($"EmailEnabled: {Value(email,"enabled")}")
            .AppendLine($"OpenAIConfigured: {Value(ai,"configured")}")
            .AppendLine($"Environment: {Safe(Value(api,"environment"))}");
        return b.ToString();
    }

    private static string Value(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return "—";
        return Safe(value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText());
    }
    private static string Safe(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "—";
        var text = value.Replace('\r',' ').Replace('\n',' ').Trim();
        if (text.Length > 200 || text.Contains('@')
            || text.Contains("password", StringComparison.OrdinalIgnoreCase)
            || text.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || text.Contains("token", StringComparison.OrdinalIgnoreCase)
            || HasLongDigitSequence(text)) return "[redacted]";
        return text;
    }
    private static bool HasLongDigitSequence(string value)
    {
        var count = 0;
        foreach (var character in value)
        {
            count = char.IsDigit(character) ? count + 1 : 0;
            if (count >= 10) return true;
        }
        return false;
    }
    private static ComponentDto Component(string name, string status, string detail) => new(name, status, detail);
    private static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value, JsonOptions);
    private sealed record ComponentDto(string name, string status, string detail);
    internal static string EmailStatus(bool enabled, bool configured, int successes, int failures)
    {
        if (!enabled) return "Disabled";
        if (!configured) return "Unhealthy";
        var total = successes + failures;
        if (total < 20) return failures >= 5 ? "Degraded" : "Healthy";
        var rate = 100d * failures / total;
        return rate > 20 ? "Unhealthy" : rate >= 5 ? "Degraded" : "Healthy";
    }
    private static string GlobalStatus(IEnumerable<ComponentDto> components)
    {
        var rows = components.ToArray();
        if (rows.Any(x => x.status == "Unhealthy")) return "Unhealthy";
        var operational = new[] { "API", "PostgreSQL", "WhatsApp", "Workers" };
        return rows.Any(x => operational.Contains(x.name) && x.status == "Degraded")
            ? "Degraded" : "Healthy";
    }
    internal static string WorkerStatus(CondoLink.Domain.Entities.WorkerHeartbeat x, DateTime now) => !x.Enabled ? "Disabled" : x.LastHeartbeatAt == default ? "Unknown" : now - x.LastHeartbeatAt > TimeSpan.FromSeconds(x.ExpectedIntervalSeconds * 5) ? "Unhealthy" : now - x.LastHeartbeatAt > TimeSpan.FromSeconds(x.ExpectedIntervalSeconds * 2.5) || x.LastSucceeded == false ? "Degraded" : "Healthy";
    internal static bool IsActiveInstance(CondoLink.Domain.Entities.WorkerHeartbeat x, DateTime now) =>
        x.LastHeartbeatAt != default && now - x.LastHeartbeatAt <= TimeSpan.FromSeconds(Math.Max(1, x.ExpectedIntervalSeconds) * 30L);
    // Kept behind the existing call sites, but scoped to this API process instead
    // of the operating-system monotonic clock (which may survive deployments).
    private static class Environment
    {
        internal static long TickCount64 => Math.Max(0,
            (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime
                .ToUniversalTime()).TotalMilliseconds);
    }
}
