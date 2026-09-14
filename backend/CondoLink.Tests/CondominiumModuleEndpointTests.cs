using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.Overwatch.Condominiums;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class CondominiumModuleEndpointTests
{
    [Fact]
    public async Task Condominium_module_api_excludes_employee_management_and_rejects_legacy_toggle()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapCondominiumModuleEndpoints(),
            builder => builder.Services.AddScoped<ICondominiumModuleService, CondominiumModuleService>());
        var condo = new Condominium("Teste", null, null);
        await host.WithDbAsync(async db => { db.Condominiums.Add(condo); CondominiumModuleService.AddDefaults(db, condo.Id, DateTime.UtcNow); await db.SaveChangesAsync(); });
        var admin = host.ClientFor(Guid.NewGuid()); admin.DefaultRequestHeaders.Add("X-Test-Role", DependencyInjection.PlatformAdminRole);
        var get = await admin.GetFromJsonAsync<JsonElement>($"/overwatch/condominiums/{condo.Id}/modules");
        Assert.DoesNotContain(get.EnumerateArray(), x => x.GetProperty("module").GetString() == "EmployeeManagement");
        var put = await admin.PutAsJsonAsync($"/overwatch/condominiums/{condo.Id}/modules", new CondominiumModuleEndpoints.UpdateModulesRequest([
            new("EmployeeManagement", false, true)]));
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }
}
