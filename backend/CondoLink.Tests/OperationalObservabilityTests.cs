using System.Net;
using System.Text.Json;
using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.Overwatch;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using CondoLink.Infrastructure.Persistence;

namespace CondoLink.Tests;

public sealed class OperationalObservabilityTests
{
    [Theory]
    [InlineData(52, 1, "Healthy")]
    [InlineData(19, 1, "Degraded")]
    [InlineData(15, 5, "Unhealthy")]
    [InlineData(0, 1, "Healthy")]
    public void Email_health_uses_rate_and_protects_small_samples(
        int successes, int failures, string expected)
        => Assert.Equal(expected,
            GetSystemStatus.EmailStatus(true, true, successes, failures));

    [Theory]
    [InlineData("/condominiums/{id}/setup/confirm", true)]
    [InlineData("/reports/export.pdf", true)]
    [InlineData("/requests/{id}", false)]
    public void Api_metrics_classify_heavy_operations(string route, bool expected)
        => Assert.Equal(expected, ApiRequestMetrics.IsHeavy(route));

    [Fact]
    public void Inactivity_worker_only_monitors_unconfirmed_residential_drafts()
    {
        Assert.Contains(WhatsAppConversationState.CollectingDescription,
            WhatsAppConversationInactivityWorker.DraftStates);
        Assert.Contains(WhatsAppConversationState.ReviewingNewRequest,
            WhatsAppConversationInactivityWorker.DraftStates);
        Assert.DoesNotContain(WhatsAppConversationState.CollectingResidentReply,
            WhatsAppConversationInactivityWorker.DraftStates);
        Assert.DoesNotContain(WhatsAppConversationState.AwaitingClosureConfirmation,
            WhatsAppConversationInactivityWorker.DraftStates);
    }

    [Fact]
    public void Valid_interaction_restarts_persisted_draft_inactivity_clock()
    {
        var started = new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc);
        var session = new WhatsAppSession("+5544999999999", started,
            started.AddMinutes(30));
        session.BeginDescription(started, started.AddMinutes(30));
        var interaction = started.AddMinutes(7);
        session.Touch(interaction, interaction.AddMinutes(30));

        Assert.Equal(interaction, session.LastInteractionAt);
        Assert.True(session.LastInteractionAt > started.AddMinutes(6));
    }

    [Theory]
    [InlineData(false, 10, 0, "Disabled")]
    [InlineData(true, 10, 20, "Healthy")]
    [InlineData(true, 10, 30, "Degraded")]
    [InlineData(true, 10, 60, "Unhealthy")]
    public void Worker_health_uses_enabled_and_individual_frequency(bool enabled, int interval, int age, string expected)
    {
        var row = new WorkerHeartbeat("Worker", "instance", enabled, interval);
        row.Beat(DateTime.UtcNow.AddSeconds(-age), enabled, interval);
        Assert.Equal(expected, GetSystemStatus.WorkerStatus(row, DateTime.UtcNow));
    }

    [Fact]
    public void Heartbeats_from_multiple_instances_do_not_collide()
    {
        var first = new WorkerHeartbeat("Worker", "node-a", true, 10);
        var second = new WorkerHeartbeat("Worker", "node-b", true, 10);
        Assert.NotEqual(first.Id, second.Id); Assert.NotEqual(first.InstanceId, second.InstanceId);
    }

    [Fact]
    public void Active_worker_window_uses_each_workers_expected_interval()
    {
        var now = DateTime.UtcNow;
        var current = new WorkerHeartbeat("Worker", "current", true, 10);
        current.Beat(now.AddSeconds(-20), true, 10);
        var old = new WorkerHeartbeat("Worker", "old-deployment", true, 10);
        old.Beat(now.AddMinutes(-10), true, 10);
        Assert.True(GetSystemStatus.IsActiveInstance(current, now));
        Assert.False(GetSystemStatus.IsActiveInstance(old, now));
        Assert.True(OperationalRetentionWorker.ShouldRemoveHeartbeat(old,
            now.AddDays(8)));
        Assert.False(OperationalRetentionWorker.ShouldRemoveHeartbeat(current, now));
    }

    [Fact]
    public void Operational_reason_is_reduced_to_safe_code()
        => Assert.Equal("invalid_reason_code", OperationalTelemetry.SafeCode("timeout: user@example.com"));

    [Fact]
    public void Api_metrics_aggregate_latency_errors_payload_and_query_counts()
    {
        var now = DateTime.UtcNow;
        var metrics = new ApiRequestMetrics();
        metrics.Record(new(now.AddMinutes(-2), "GET", "/requests/{id}", 200, 100, 1200, 2, 0, 15, 10));
        metrics.Record(new(now.AddMinutes(-1), "GET", "/requests/{id}", 500, 900, 1800, 6, 1, 700, 600));

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(metrics.Performance(now)));
        var oneHour = json.RootElement.GetProperty("periods")[0];
        var endpoint = json.RootElement.GetProperty("topSlowest")[0];
        Assert.Equal(2, oneHour.GetProperty("requests").GetInt32());
        Assert.Equal(500, oneHour.GetProperty("averageMs").GetDouble());
        Assert.Equal(900, oneHour.GetProperty("p95Ms").GetDouble());
        Assert.Equal(1, oneHour.GetProperty("errors5xx").GetInt32());
        Assert.Equal(4, oneHour.GetProperty("averageQueries").GetDouble());
        Assert.Equal("/requests/{id}", endpoint.GetProperty("Route").GetString());
        Assert.Equal(6, endpoint.GetProperty("MaximumQueries").GetInt32());
    }

    [Fact]
    public void Query_scope_counts_commands_and_restores_outer_scope()
    {
        var scope = new QueryPerformanceScope();
        using (scope.Begin())
        {
            scope.Record(25);
            scope.Record(600);
            Assert.Equal(new QueryPerformanceSnapshot(2, 1, 625, 600), scope.Snapshot());
        }
        Assert.Equal(new QueryPerformanceSnapshot(0, 0, 0, 0), scope.Snapshot());
    }

    [Fact]
    public async Task System_endpoint_allows_only_platform_admin()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(app => app.MapGetSystemStatus(), builder =>
        {
            builder.Services.Configure<WhatsAppOptions>(x => x.AccessToken = "wa_secret_value");
            builder.Services.Configure<EmailOptions>(x => x.Password = "smtp_secret_value");
            builder.Services.Configure<RequestDraftAiOptions>(x => x.ApiKey = "ai_secret_value");
            builder.Services.AddSingleton<ApiRequestMetrics>();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.AnonymousClient().GetAsync("/overwatch/system")).StatusCode);
        var resident = host.ClientFor(Guid.NewGuid()); resident.DefaultRequestHeaders.Add("X-Test-Role", "Resident");
        Assert.Equal(HttpStatusCode.Forbidden, (await resident.GetAsync("/overwatch/system")).StatusCode);
        var manager = host.ClientFor(Guid.NewGuid()); manager.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/overwatch/system")).StatusCode);
        var admin = host.ClientFor(Guid.NewGuid()); admin.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/overwatch/system")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.AnonymousClient().GetAsync("/overwatch/system/diagnostic")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await resident.GetAsync("/overwatch/system/diagnostic")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/overwatch/system/diagnostic")).StatusCode);
        var diagnostic = await admin.GetAsync("/overwatch/system/diagnostic");
        Assert.Equal(HttpStatusCode.OK, diagnostic.StatusCode);
        Assert.Equal("text/plain", diagnostic.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", diagnostic.Content.Headers.ContentType?.CharSet);
        Assert.Matches("comvy-diagnostico-\\d{4}-\\d{2}-\\d{2}-\\d{6}\\.txt",
            diagnostic.Content.Headers.ContentDisposition?.FileName ?? "");
        var text = await diagnostic.Content.ReadAsStringAsync();
        Assert.Contains("STATUS GERAL", text);
        Assert.Contains("WORKERS", text);
        Assert.Contains("PERFORMANCE DA API", text);
        Assert.Contains("EVENTOS OPERACIONAIS RECENTES", text);
        Assert.DoesNotContain("connection string", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("wa_secret_value", text);
        Assert.DoesNotContain("smtp_secret_value", text);
        Assert.DoesNotContain("ai_secret_value", text);
    }

    [Fact]
    public async Task Old_instance_is_hidden_and_does_not_override_current_worker_health()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapGetSystemStatus(), builder =>
            {
                builder.Services.Configure<WhatsAppOptions>(_ => { });
                builder.Services.Configure<EmailOptions>(_ => { });
                builder.Services.Configure<RequestDraftAiOptions>(_ => { });
                builder.Services.AddSingleton<ApiRequestMetrics>();
            });
        var now = DateTime.UtcNow;
        await host.WithDbAsync(async db =>
        {
            var current = new WorkerHeartbeat("WhatsAppOutboundWorker", "current", true, 10);
            current.Beat(now.AddSeconds(-5), true, 10);
            var old = new WorkerHeartbeat("WhatsAppOutboundWorker", "old-container", true, 10);
            old.Beat(now.AddDays(-8), true, 10);
            var closure = new WorkerHeartbeat("RequestClosureWorker", "current", true, 10);
            closure.Beat(now.AddSeconds(-5), true, 10);
            var inactivity = new WorkerHeartbeat("WhatsAppConversationInactivityWorker", "current", true, 10);
            inactivity.Beat(now.AddSeconds(-5), true, 10);
            var retention = new WorkerHeartbeat("OperationalRetentionWorker", "current", true, 86400);
            retention.Beat(now.AddMinutes(-1), true, 86400);
            db.AddRange(current, old, closure, inactivity, retention, new OperationalEvent(now, "Workers",
                "Execution", "Error", "safe_failure", "request-123"),
                new OperationalEvent(now, "Email", "Send", "Error",
                    "resident@example.com", "5511999999999"));
            await db.SaveChangesAsync();
        });
        var admin = host.ClientFor(Guid.NewGuid());
        admin.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        using var json = JsonDocument.Parse(await admin.GetStringAsync("/overwatch/system"));
        var workers = json.RootElement.GetProperty("workers").EnumerateArray().ToArray();
        Assert.Equal(4, workers.Length);
        Assert.DoesNotContain(workers,
            x => x.GetProperty("instanceId").GetString() == "old-container");
        var workerComponent = json.RootElement.GetProperty("components")
            .EnumerateArray().Single(x => x.GetProperty("name").GetString() == "Workers");
        Assert.Equal("Healthy", workerComponent.GetProperty("status").GetString());
        var diagnostic = await admin.GetStringAsync("/overwatch/system/diagnostic");
        Assert.Contains("safe_failure", diagnostic);
        Assert.Contains("request-123", diagnostic);
        Assert.DoesNotContain("old-container", diagnostic);
        Assert.DoesNotContain("resident@example.com", diagnostic);
        Assert.DoesNotContain("5511999999999", diagnostic);
        Assert.Contains("[redacted]", diagnostic);
        Assert.Equal(1, await host.WithDbAsync(db =>
            OperationalRetentionWorker.DeleteExpiredHeartbeatsAsync(db, now)));
        Assert.Equal(4, (await host.WithDbAsync(db =>
            db.WorkerHeartbeats.AsNoTracking().ToArrayAsync())).Length);
    }
}
