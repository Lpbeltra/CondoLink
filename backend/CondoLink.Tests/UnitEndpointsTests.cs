using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Units;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

/// <summary>
/// Endpoint-level guarantees for the unit registry: it is manager-only, blocks
/// must belong to the same condominium, identifiers are unique per
/// condominium+block, and GET /units/{id} resolves the condominium from the
/// unit row before authorising the caller.
/// </summary>
public sealed class UnitEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;

    private Guid _condominiumId;
    private Guid _otherCondominiumId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _residentId;
    private Guid _foreignBlockId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(application =>
        {
            application.MapCreateUnit();
            application.MapManageUnit();
            application.MapGetUnitById();
            application.MapListCondominiumUnits();
        });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var otherCondominium = new Condominium("Residencial Beta", null, null);
            var foreignBlock = new CondominiumBlock(otherCondominium.Id, "Torre X");
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var otherManager = CoreTestSeed.User("Sindico Beta", "beta@example.com");
            var resident = CoreTestSeed.User("Morador", "morador@example.com");

            db.AddRange(
                condominium, otherCondominium, foreignBlock,
                manager, otherManager, resident);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, otherManager.Id, otherCondominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, resident.Id, condominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();

            _condominiumId = condominium.Id;
            _otherCondominiumId = otherCondominium.Id;
            _managerId = manager.Id;
            _otherManagerId = otherManager.Id;
            _residentId = resident.Id;
            _foreignBlockId = foreignBlock.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Manager_can_create_a_unit_and_receives_201()
    {
        var response = await CreateAsync(_managerId, "  101  ", floor: " 1 ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CreateUnit.Response>();
        Assert.Equal("101", body!.Identifier);
        Assert.Equal("1", body.Floor);
        Assert.Null(body.BlockId);
        Assert.True(body.IsActive);
        Assert.Equal(_condominiumId, body.CondominiumId);
    }

    [Fact]
    public async Task Resident_cannot_create_a_unit()
    {
        var response = await CreateAsync(_residentId, "101");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await UnitCountAsync());
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_create_a_unit()
    {
        var response = await CreateAsync(_otherManagerId, "101");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await UnitCountAsync());
    }

    [Fact]
    public async Task Anonymous_caller_cannot_create_a_unit()
    {
        var response = await _host.AnonymousClient().PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/units",
            new { identifier = "101" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_identifier_in_the_same_condominium_returns_409()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync(_managerId, "101")).StatusCode);

        var response = await CreateAsync(_managerId, " 101 ");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await UnitCountAsync());
    }

    [Fact]
    public async Task Block_from_another_condominium_is_rejected_with_400()
    {
        var response = await CreateAsync(
            _managerId, "101", blockId: _foreignBlockId);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await UnitCountAsync());
    }

    [Fact]
    public async Task Block_is_required_once_the_condominium_has_registered_blocks()
    {
        var blockId = await AddBlockAsync("Torre A");

        var withoutBlock = await CreateAsync(_managerId, "101");
        Assert.Equal(HttpStatusCode.BadRequest, withoutBlock.StatusCode);

        var withBlock = await CreateAsync(_managerId, "101", blockId: blockId);
        Assert.Equal(HttpStatusCode.Created, withBlock.StatusCode);
        var body = await withBlock.Content
            .ReadFromJsonAsync<CreateUnit.Response>();
        Assert.Equal(blockId, body!.BlockId);
        Assert.Equal("Torre A", body.Block);
    }

    [Fact]
    public async Task Same_identifier_in_two_different_blocks_is_allowed()
    {
        var firstBlockId = await AddBlockAsync("Torre A");
        var secondBlockId = await AddBlockAsync("Torre B");

        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync(_managerId, "101", blockId: firstBlockId))
            .StatusCode);
        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync(_managerId, "101", blockId: secondBlockId))
            .StatusCode);
        Assert.Equal(2, await UnitCountAsync());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Missing_identifier_returns_400(string? identifier)
    {
        var response = await CreateAsync(_managerId, identifier);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Identifier_longer_than_50_characters_returns_400()
    {
        var response = await CreateAsync(_managerId, new string('a', 51));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Description_longer_than_500_characters_returns_400()
    {
        var response = await CreateAsync(
            _managerId, "101", description: new string('a', 501));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_condominium_cannot_receive_new_units()
    {
        await _host.WithDbAsync(async db =>
        {
            var condominium = await db.Condominiums
                .SingleAsync(item => item.Id == _condominiumId);
            condominium.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await CreateAsync(_managerId, "101");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Creating_a_unit_in_a_missing_condominium_returns_403()
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{Guid.NewGuid()}/units",
            new { identifier = "101" });

        // The manager check runs first, so an unknown condominium is
        // indistinguishable from one the caller does not manage.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_rename_a_unit()
    {
        var unitId = await CreatedUnitIdAsync("101");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/units/{unitId}",
            new { identifier = "  102  ", description = " Fundos " });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var unit = await _host.WithDbAsync(db => db.Units
            .AsNoTracking().SingleAsync(item => item.Id == unitId));
        Assert.Equal("102", unit.Identifier);
        Assert.Equal("Fundos", unit.Description);
    }

    [Fact]
    public async Task Renaming_a_unit_onto_an_existing_identifier_returns_409()
    {
        var firstId = await CreatedUnitIdAsync("101");
        await CreatedUnitIdAsync("102");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/units/{firstId}",
            new { identifier = "102" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("101", await _host.WithDbAsync(db => db.Units
            .AsNoTracking().Where(item => item.Id == firstId)
            .Select(item => item.Identifier).SingleAsync()));
    }

    [Fact]
    public async Task Renaming_a_unit_to_its_own_identifier_is_accepted()
    {
        var unitId = await CreatedUnitIdAsync("101");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/units/{unitId}",
            new { identifier = "101" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Resident_cannot_update_or_delete_a_unit()
    {
        var unitId = await CreatedUnitIdAsync("101");
        var resident = _host.ClientFor(_residentId);

        var update = await resident.PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/units/{unitId}",
            new { identifier = "999" });
        var delete = await resident.DeleteAsync(
            $"/condominiums/{_condominiumId}/units/{unitId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(1, await UnitCountAsync());
    }

    [Fact]
    public async Task Updating_a_unit_of_another_condominium_returns_404()
    {
        var unitId = await CreatedUnitIdAsync("101");

        var response = await _host.ClientFor(_otherManagerId).PutAsJsonAsync(
            $"/condominiums/{_otherCondominiumId}/units/{unitId}",
            new { identifier = "999" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_delete_an_unlinked_unit()
    {
        var unitId = await CreatedUnitIdAsync("101");

        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/units/{unitId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await UnitCountAsync());
    }

    [Fact]
    public async Task Deleting_a_unit_that_has_people_linked_returns_409()
    {
        var unitId = await CreatedUnitIdAsync("101");
        await _host.WithDbAsync(async db =>
        {
            db.UnitMemberships.Add(new UnitMembership(
                _residentId, unitId, UnitRelationshipType.Owner, true, true));
            await db.SaveChangesAsync();
        });

        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/units/{unitId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await UnitCountAsync());
    }

    [Fact]
    public async Task Deleting_a_unit_that_has_requests_returns_409()
    {
        var unitId = await CreatedUnitIdAsync("101");
        await _host.WithDbAsync(async db =>
        {
            var category = new Category(_condominiumId, "Manutenção", null);
            db.Categories.Add(category);
            db.Requests.Add(new DomainRequest(
                _condominiumId, _residentId, unitId, category.Id,
                "Vazamento", "Descrição"));
            await db.SaveChangesAsync();
        });

        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/units/{unitId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_missing_unit_returns_404()
    {
        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/units/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_read_a_unit_by_id()
    {
        var unitId = await CreatedUnitIdAsync("101");

        var body = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<GetUnitById.Response>($"/units/{unitId}");

        Assert.Equal(unitId, body!.Id);
        Assert.Equal("101", body.Identifier);
        Assert.Equal(_condominiumId, body.CondominiumId);
    }

    [Fact]
    public async Task Resident_cannot_read_a_unit_by_id()
    {
        var unitId = await CreatedUnitIdAsync("101");

        var response = await _host.ClientFor(_residentId)
            .GetAsync($"/units/{unitId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_unit_by_id_resolves_the_condominium_from_the_unit_row()
    {
        var unitId = await CreatedUnitIdAsync("101");

        // The other manager manages a different condominium, so even though the
        // route carries no condominium id the handler must still refuse.
        var response = await _host.ClientFor(_otherManagerId)
            .GetAsync($"/units/{unitId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Reading_a_missing_unit_returns_404_before_the_manager_check()
    {
        var response = await _host.ClientFor(_residentId)
            .GetAsync($"/units/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manager_lists_only_the_units_of_their_condominium_with_people_counts()
    {
        var firstId = await CreatedUnitIdAsync("101");
        await CreatedUnitIdAsync("102");
        await _host.WithDbAsync(async db =>
        {
            db.Units.Add(new Unit(_otherCondominiumId, "999", null, null, null));
            db.UnitMemberships.Add(new UnitMembership(
                _residentId, firstId, UnitRelationshipType.Owner, true, true));
            await db.SaveChangesAsync();
        });

        var units = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<List<ListCondominiumUnits.Response>>(
                $"/condominiums/{_condominiumId}/units") ?? [];

        Assert.Equal(2, units.Count);
        Assert.All(units,
            unit => Assert.Equal(_condominiumId, unit.CondominiumId));
        Assert.Equal(1, units.Single(unit => unit.Id == firstId).PeopleCount);
        Assert.Equal(0, units.Single(unit => unit.Identifier == "102").PeopleCount);
    }

    [Fact]
    public async Task Resident_cannot_list_condominium_units()
    {
        var response = await _host.ClientFor(_residentId)
            .GetAsync($"/condominiums/{_condominiumId}/units");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listing_units_of_a_condominium_the_caller_does_not_manage_is_forbidden()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/condominiums/{_otherCondominiumId}/units");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<HttpResponseMessage> CreateAsync(
        Guid callerId,
        string? identifier,
        Guid? blockId = null,
        string? floor = null,
        string? description = null) =>
        _host.ClientFor(callerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/units",
            new { identifier, blockId, floor, description });

    private async Task<Guid> CreatedUnitIdAsync(string identifier)
    {
        var response = await CreateAsync(_managerId, identifier);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CreateUnit.Response>();
        return body!.Id;
    }

    private Task<Guid> AddBlockAsync(string identifier) =>
        _host.WithDbAsync(async db =>
        {
            var block = new CondominiumBlock(_condominiumId, identifier);
            db.CondominiumBlocks.Add(block);
            await db.SaveChangesAsync();
            return block.Id;
        });

    private Task<int> UnitCountAsync() =>
        _host.WithDbAsync(db => db.Units
            .CountAsync(unit => unit.CondominiumId == _condominiumId));
}
