using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.Management;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

public sealed class ManagementModulesEndpointsTests
{
    [Fact]
    public async Task Modules_follow_active_administrative_context_and_never_accept_client_condominium_id()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(
            app => app.MapManagementContext(),
            builder => builder.Services.AddScoped<ICondominiumModuleService, CondominiumModuleService>());
        var user = CoreTestSeed.User("Manager", "modules@test.local");
        var first = new Condominium("A", null, null);
        var second = new Condominium("B", null, null);
        await host.WithDbAsync(async db =>
        {
            db.AddRange(user, first, second);
            CoreTestSeed.AddMember(db, user.Id, first.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, user.Id, second.Id, CondominiumRole.Manager);
            CondominiumModuleService.AddDefaults(db, first.Id, DateTime.UtcNow);
            CondominiumModuleService.AddDefaults(db, second.Id, DateTime.UtcNow);
            db.CondominiumModules.Local.Single(x => x.CondominiumId == second.Id && x.Module == CondominiumModuleType.Assistant).Set(false, false, DateTime.UtcNow);
            await db.SaveChangesAsync();
        });
        var client = host.ClientFor(user.Id);

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/management/context", new { condominiumId = first.Id })).StatusCode);
        var firstModules = await client.GetFromJsonAsync<Response>("/management/modules");
        Assert.True(firstModules!.Modules.Single(x => x.Module == "Assistant").Enabled);

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/management/context", new { condominiumId = second.Id })).StatusCode);
        var secondModules = await client.GetFromJsonAsync<Response>("/management/modules?condominiumId=" + first.Id);
        Assert.False(secondModules!.Modules.Single(x => x.Module == "Assistant").Enabled);
        Assert.All(secondModules.Modules, x => Assert.True(x.Module.Length > 0));

        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/management/context", new { condominiumId = (Guid?)null })).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<Response>("/management/modules"))!.Modules);
    }

    [Fact]
    public async Task User_without_administrative_access_gets_no_modules()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(app => app.MapManagementContext(), builder => builder.Services.AddScoped<ICondominiumModuleService, CondominiumModuleService>());
        var user = CoreTestSeed.User("Resident", "resident-modules@test.local");
        await host.WithDbAsync(async db => { db.Users.Add(user); await db.SaveChangesAsync(); });
        var response = await host.ClientFor(user.Id).GetFromJsonAsync<Response>("/management/modules");
        Assert.Empty(response!.Modules);
    }

    private sealed record Response(IReadOnlyList<Item> Modules);
    private sealed record Item(string Module, bool Enabled);
}
