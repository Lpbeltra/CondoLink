using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.Overwatch.Condominiums;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CondoLink.Tests;

public sealed class CondominiumModuleEndpointTests
{
    [Fact]
    public async Task Overwatch_module_endpoints_allow_only_platform_admin_and_normalize_unsupported_delegation()
    {
        var logs = new RecordingLoggerProvider();
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapCondominiumModuleEndpoints(),
            builder => { builder.Services.AddScoped<ICondominiumModuleService, CondominiumModuleService>(); builder.Logging.AddProvider(logs); });
        var condominium = new Condominium("Teste", null, null);
        await host.WithDbAsync(async db =>
        {
            db.Condominiums.Add(condominium);
            CondominiumModuleService.AddDefaults(db, condominium.Id, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });

        foreach (var role in new[] { "Manager", "SubManager", "ManagementCompanyEmployee", "Resident" })
        {
            var denied = host.ClientFor(Guid.NewGuid());
            denied.DefaultRequestHeaders.Add("X-Test-Role", role);
            Assert.Equal(HttpStatusCode.Forbidden, (await denied.GetAsync($"/overwatch/condominiums/{condominium.Id}/modules")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await denied.PutAsJsonAsync($"/overwatch/condominiums/{condominium.Id}/modules", new CondominiumModuleEndpoints.UpdateModulesRequest([]))).StatusCode);
        }

        var admin = host.ClientFor(Guid.NewGuid());
        admin.DefaultRequestHeaders.Add("X-Test-Role", DependencyInjection.PlatformAdminRole);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/overwatch/condominiums/{condominium.Id}/modules")).StatusCode);

        var update = new CondominiumModuleEndpoints.UpdateModulesRequest([
            new CondominiumModuleEndpoints.ModuleUpdate("Documents", true, true),
            new CondominiumModuleEndpoints.ModuleUpdate("EmployeeManagement", false, true)
        ]);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/overwatch/condominiums/{condominium.Id}/modules", update)).StatusCode);

        var states = await host.WithDbAsync(db => Task.FromResult(db.CondominiumModules
            .Where(x => x.CondominiumId == condominium.Id).ToArray()));
        Assert.False(states.Single(x => x.Module.ToString() == "Documents").ManagementCompanyAccessEnabled);
        Assert.False(states.Single(x => x.Module.ToString() == "EmployeeManagement").ManagementCompanyAccessEnabled);
        var audit = Assert.Single(logs.Entries.Where(x => x.Category == "CondominiumModuleAudit" && x.Values.TryGetValue("Module", out var module) && module?.ToString() == "Documents"));
        Assert.Equal(condominium.Id, audit.Values["CondominiumId"]);
        Assert.Equal("Documents", audit.Values["Module"]?.ToString());
        Assert.True(audit.Values.ContainsKey("IsEnabledBefore") && audit.Values.ContainsKey("IsEnabledAfter"));
        Assert.True(audit.Values.ContainsKey("ManagementCompanyAccessBefore") && audit.Values.ContainsKey("ManagementCompanyAccessAfter"));
        Assert.True(audit.Values.ContainsKey("PlatformAdmin") && audit.Values.ContainsKey("Timestamp"));
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public List<Entry> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(categoryName, Entries);
        public void Dispose() { }
    }
    private sealed class RecordingLogger(string category, List<Entry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state as IReadOnlyList<KeyValuePair<string, object?>> ?? [];
            entries.Add(new Entry(category, values.ToDictionary(x => x.Key, x => x.Value)));
        }
    }
    private sealed record Entry(string Category, IReadOnlyDictionary<string, object?> Values);
}
