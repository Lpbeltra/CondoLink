using System.Net;
using System.Text.Json;
using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.Overwatch;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using CondoLink.Infrastructure.Persistence;

namespace CondoLink.Tests;

public sealed class OperationalObservabilityTests
{
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
            builder.Services.Configure<WhatsAppOptions>(_ => { });
            builder.Services.Configure<EmailOptions>(_ => { });
            builder.Services.Configure<RequestDraftAiOptions>(_ => { });
            builder.Services.AddSingleton<ApiRequestMetrics>();
        });
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.AnonymousClient().GetAsync("/overwatch/system")).StatusCode);
        var resident = host.ClientFor(Guid.NewGuid()); resident.DefaultRequestHeaders.Add("X-Test-Role", "Resident");
        Assert.Equal(HttpStatusCode.Forbidden, (await resident.GetAsync("/overwatch/system")).StatusCode);
        var manager = host.ClientFor(Guid.NewGuid()); manager.DefaultRequestHeaders.Add("X-Test-Role", "Manager");
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.GetAsync("/overwatch/system")).StatusCode);
        var admin = host.ClientFor(Guid.NewGuid()); admin.DefaultRequestHeaders.Add("X-Test-Role", "PlatformAdmin");
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/overwatch/system")).StatusCode);
    }
}
