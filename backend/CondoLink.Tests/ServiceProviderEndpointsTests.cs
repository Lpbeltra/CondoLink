using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CondoLink.Api.Features.ServiceProviders;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

public sealed class ServiceProviderEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;
    private Guid _managerId;
    private Guid _condominiumId;
    private Guid _otherCondominiumId;
    private Guid _sharedProviderId;
    private Guid _subManagerId;
    private Guid _subManagerDeniedId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(app => app.MapServiceProviderEndpoints());
        await _host.WithDbAsync(async db =>
        {
            var manager = CoreTestSeed.User("Síndico", "provider-manager@test.local");
            var subManager = CoreTestSeed.User("Subsíndico", "provider-sub@test.local");
            var deniedSubManager = CoreTestSeed.User("Subsíndico sem acesso", "provider-sub-denied@test.local");
            var condominium = new Condominium("Monticello", null, null);
            var other = new Condominium("Montpellier", null, null);
            _managerId = manager.Id;
            _condominiumId = condominium.Id;
            _otherCondominiumId = other.Id;
            CoreTestSeed.AddMember(db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(db, manager.Id, other.Id, CondominiumRole.Manager);
            var subMembership = CoreTestSeed.AddMember(db, subManager.Id, condominium.Id, CondominiumRole.SubManager);
            var deniedMembership = CoreTestSeed.AddMember(db, deniedSubManager.Id, condominium.Id, CondominiumRole.SubManager);
            db.SubManagerModulePermissions.Add(new(subMembership.Id, SubManagerModule.Management, manager.Id, true));
            db.SubManagerModulePermissions.Add(new(deniedMembership.Id, SubManagerModule.Management, manager.Id, false));
            _subManagerId = subManager.Id;
            _subManagerDeniedId = deniedSubManager.Id;

            var personal = Provider("Pessoal");
            var selected = Provider("Monticello");
            var otherOnly = Provider("Montpellier");
            var shared = Provider("Compartilhado");
            _sharedProviderId = shared.Id;
            db.AddRange(manager, subManager, deniedSubManager, condominium, other, personal, selected, otherOnly, shared,
                new ServiceProviderUserLink(personal.Id, manager.Id),
                new ServiceProviderCondominiumLink(selected.Id, condominium.Id),
                new ServiceProviderCondominiumLink(otherOnly.Id, other.Id),
                new ServiceProviderCondominiumLink(shared.Id, condominium.Id),
                new ServiceProviderCondominiumLink(shared.Id, other.Id));
            await db.SaveChangesAsync();
        });
    }

    public Task DisposeAsync() => _host.DisposeAsync().AsTask();

    [Fact]
    public async Task Specific_context_returns_personal_and_selected_condominium_without_other_condominium()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/management/service-providers?condominiumId={_condominiumId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var names = await NamesAsync(response);
        Assert.Equal(["Compartilhado", "Monticello", "Pessoal"], names);
    }

    [Fact]
    public async Task Omitted_context_is_consolidated_without_duplicate_shared_provider()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync("/management/service-providers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var names = await NamesAsync(response);
        Assert.Equal(["Compartilhado", "Monticello", "Montpellier", "Pessoal"], names);
        Assert.Single(names, name => name == "Compartilhado");
    }

    [Fact]
    public async Task Update_in_specific_context_preserves_link_outside_that_context()
    {
        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/management/service-providers/{_sharedProviderId}?condominiumId={_condominiumId}",
            new
            {
                name = "Compartilhado atualizado",
                companyName = (string?)null,
                specialties = new[] { "Elétrica" },
                phone = "11999999999",
                email = (string?)null,
                pixKey = (string?)null,
                pixKeyType = (string?)null,
                notes = (string?)null,
                isMine = false,
                condominiumIds = Array.Empty<Guid>(),
                isActive = true
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _host.WithDbAsync(async db =>
        {
            Assert.False(await db.ServiceProviderCondominiumLinks.AnyAsync(x =>
                x.ServiceProviderId == _sharedProviderId && x.CondominiumId == _condominiumId));
            Assert.True(await db.ServiceProviderCondominiumLinks.AnyAsync(x =>
                x.ServiceProviderId == _sharedProviderId && x.CondominiumId == _otherCondominiumId));
        });
    }

    [Fact]
    public async Task Unmanaged_specific_context_is_forbidden()
    {
        var outsider = CoreTestSeed.User("Outro", "provider-outsider@test.local");
        await _host.WithDbAsync(async db => { db.Users.Add(outsider); await db.SaveChangesAsync(); });

        var response = await _host.ClientFor(outsider.Id)
            .GetAsync($"/management/service-providers?condominiumId={_condominiumId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Submanager_with_management_permission_can_list()
    {
        var response = await _host.ClientFor(_subManagerId).GetAsync(
            $"/management/service-providers?condominiumId={_condominiumId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Submanager_without_management_permission_is_forbidden()
    {
        var response = await _host.ClientFor(_subManagerDeniedId).GetAsync(
            $"/management/service-providers?condominiumId={_condominiumId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Status_and_mine_filters_are_server_side()
    {
        var response = await _host.ClientFor(_managerId).GetAsync(
            "/management/service-providers?status=active&availability=mine");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["Pessoal"], await NamesAsync(response));
    }

    private static ServiceProvider Provider(string name) =>
        new(name, null, "Manutenção", null, "11999999999", null, null, null, null, DateTime.UtcNow);

    private static async Task<List<string>> NamesAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()!)
            .ToList();
    }
}
