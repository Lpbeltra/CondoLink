using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Blocks;
using CondoLink.Api.Features.CondominiumSetup;
using CondoLink.Api.Features.Units;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;

namespace CondoLink.Tests;

public sealed class StructuralAuthorizationTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _condominiumId;
    private Guid _managerId;
    private Guid _adminId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(app =>
        {
            app.MapCondominiumBlocks(); app.MapCreateUnit();
            app.MapManageUnit(); app.MapListCondominiumUnits();
            app.MapCondominiumSetup();
        });
        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Alfa", null, null);
            var manager = CoreTestSeed.User("Manager", "manager@test.local");
            var admin = CoreTestSeed.User("Admin", "admin@test.local");
            db.AddRange(condominium, manager, admin);
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            await db.SaveChangesAsync();
            _condominiumId = condominium.Id; _managerId = manager.Id; _adminId = admin.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Management_can_read_but_not_mutate_structure_while_platform_admin_can_mutate_without_membership()
    {
        var manager = _host.ClientFor(_managerId);
        Assert.Equal(HttpStatusCode.OK, (await manager.GetAsync($"/condominiums/{_condominiumId}/blocks")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/condominiums/{_condominiumId}/blocks", new { identifier = "A" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await manager.PostAsJsonAsync($"/condominiums/{_condominiumId}/setup/preview", new { noRegistrableUnits = true, units = Array.Empty<object>(), residents = Array.Empty<object>() })).StatusCode);

        var admin = _host.ClientFor(_adminId);
        admin.DefaultRequestHeaders.Add("X-Test-Role", DependencyInjection.PlatformAdminRole);
        var createBlock = await admin.PostAsJsonAsync($"/condominiums/{_condominiumId}/blocks", new { identifier = "A" });
        Assert.Equal(HttpStatusCode.Created, createBlock.StatusCode);
        var block = await createBlock.Content.ReadFromJsonAsync<CondominiumBlockEndpoints.Response>();
        Assert.Equal(HttpStatusCode.Created, (await admin.PostAsJsonAsync($"/condominiums/{_condominiumId}/units", new { identifier = "101", blockId = block!.Id, floor = (string?)null, description = (string?)null })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/condominiums/{_condominiumId}/setup/preview", new { noRegistrableUnits = true, units = Array.Empty<object>(), residents = Array.Empty<object>() })).StatusCode);
    }
}
