using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Blocks;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

/// <summary>
/// Endpoint-level guarantees for condominium blocks: CRUD is manager-only,
/// identifiers are unique per condominium case-insensitively, and a block that
/// still has units attached cannot be deleted.
/// </summary>
public sealed class CondominiumBlockEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;

    private Guid _condominiumId;
    private Guid _otherCondominiumId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _residentId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(
            application => application.MapCondominiumBlocks());

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var otherCondominium = new Condominium("Residencial Beta", null, null);
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var otherManager = CoreTestSeed.User("Sindico Beta", "beta@example.com");
            var resident = CoreTestSeed.User("Morador", "morador@example.com");

            db.AddRange(
                condominium, otherCondominium, manager, otherManager, resident);
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
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Manager_can_create_a_block_and_receives_201()
    {
        var response = await CreateAsync(_managerId, "  Torre A  ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CondominiumBlockEndpoints.Response>();
        Assert.Equal("Torre A", body!.Identifier);
        Assert.Equal(_condominiumId, body.CondominiumId);
        Assert.Equal(0, body.UnitCount);
    }

    [Fact]
    public async Task Resident_cannot_create_a_block()
    {
        var response = await CreateAsync(_residentId, "Torre A");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await BlockCountAsync());
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_create_a_block_here()
    {
        var response = await CreateAsync(_otherManagerId, "Torre A");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await BlockCountAsync());
    }

    [Fact]
    public async Task Anonymous_caller_cannot_create_a_block()
    {
        var response = await _host.AnonymousClient().PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/blocks",
            new { identifier = "Torre A" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_identifier_returns_409_case_insensitively()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync(_managerId, "Torre A")).StatusCode);

        var response = await CreateAsync(_managerId, "  torre a  ");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await BlockCountAsync());
    }

    [Fact]
    public async Task The_same_identifier_may_be_reused_in_a_different_condominium()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await CreateAsync(_managerId, "Torre A")).StatusCode);

        var response = await _host.ClientFor(_otherManagerId).PostAsJsonAsync(
            $"/condominiums/{_otherCondominiumId}/blocks",
            new { identifier = "Torre A" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
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
    public async Task Manager_can_rename_a_block_and_sees_its_unit_count()
    {
        var blockId = await CreatedBlockIdAsync("Torre A");
        await AddUnitAsync(blockId, "101");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/blocks/{blockId}",
            new { identifier = "  Torre B  " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CondominiumBlockEndpoints.Response>();
        Assert.Equal("Torre B", body!.Identifier);
        Assert.Equal(1, body.UnitCount);
    }

    [Fact]
    public async Task Renaming_a_block_onto_an_existing_identifier_returns_409()
    {
        var firstId = await CreatedBlockIdAsync("Torre A");
        await CreatedBlockIdAsync("Torre B");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/blocks/{firstId}",
            new { identifier = "torre b" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Torre A", await _host.WithDbAsync(db =>
            db.CondominiumBlocks.AsNoTracking()
                .Where(block => block.Id == firstId)
                .Select(block => block.Identifier).SingleAsync()));
    }

    [Fact]
    public async Task Renaming_a_block_to_its_own_identifier_is_accepted()
    {
        var blockId = await CreatedBlockIdAsync("Torre A");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/blocks/{blockId}",
            new { identifier = "Torre A" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Resident_cannot_rename_or_delete_a_block()
    {
        var blockId = await CreatedBlockIdAsync("Torre A");
        var resident = _host.ClientFor(_residentId);

        var update = await resident.PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/blocks/{blockId}",
            new { identifier = "Torre Z" });
        var delete = await resident.DeleteAsync(
            $"/condominiums/{_condominiumId}/blocks/{blockId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(1, await BlockCountAsync());
    }

    [Fact]
    public async Task Renaming_a_block_of_another_condominium_returns_404()
    {
        var blockId = await CreatedBlockIdAsync("Torre A");

        var response = await _host.ClientFor(_otherManagerId).PutAsJsonAsync(
            $"/condominiums/{_otherCondominiumId}/blocks/{blockId}",
            new { identifier = "Roubado" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Renaming_a_missing_block_returns_404()
    {
        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/blocks/{Guid.NewGuid()}",
            new { identifier = "Torre A" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_delete_an_empty_block()
    {
        var blockId = await CreatedBlockIdAsync("Torre A");

        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/blocks/{blockId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await BlockCountAsync());
    }

    [Fact]
    public async Task Deleting_a_block_that_still_has_units_returns_409()
    {
        var blockId = await CreatedBlockIdAsync("Torre A");
        await AddUnitAsync(blockId, "101");
        await AddUnitAsync(blockId, "102");

        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/blocks/{blockId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await BlockCountAsync());
        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.Contains("2", error!.Error);
    }

    [Fact]
    public async Task Deleting_a_block_becomes_possible_once_its_units_are_removed()
    {
        var blockId = await CreatedBlockIdAsync("Torre A");
        var unitId = await AddUnitAsync(blockId, "101");
        var manager = _host.ClientFor(_managerId);
        Assert.Equal(HttpStatusCode.Conflict, (await manager.DeleteAsync(
            $"/condominiums/{_condominiumId}/blocks/{blockId}")).StatusCode);

        await _host.WithDbAsync(async db =>
        {
            db.Units.Remove(await db.Units.SingleAsync(u => u.Id == unitId));
            await db.SaveChangesAsync();
        });

        var response = await manager.DeleteAsync(
            $"/condominiums/{_condominiumId}/blocks/{blockId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_missing_block_returns_404()
    {
        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/condominiums/{_condominiumId}/blocks/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manager_lists_only_the_blocks_of_their_condominium_with_unit_counts()
    {
        var firstId = await CreatedBlockIdAsync("Torre A");
        await CreatedBlockIdAsync("Torre B");
        await AddUnitAsync(firstId, "101");
        await _host.WithDbAsync(async db =>
        {
            db.CondominiumBlocks.Add(
                new CondominiumBlock(_otherCondominiumId, "Torre X"));
            await db.SaveChangesAsync();
        });

        var blocks = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<List<CondominiumBlockEndpoints.Response>>(
                $"/condominiums/{_condominiumId}/blocks") ?? [];

        Assert.Equal(2, blocks.Count);
        Assert.All(blocks,
            block => Assert.Equal(_condominiumId, block.CondominiumId));
        Assert.Equal(1, blocks.Single(block => block.Id == firstId).UnitCount);
        Assert.Equal(0,
            blocks.Single(block => block.Identifier == "Torre B").UnitCount);
    }

    [Fact]
    public async Task Resident_cannot_list_the_blocks()
    {
        var response = await _host.ClientFor(_residentId)
            .GetAsync($"/condominiums/{_condominiumId}/blocks");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listing_the_blocks_of_a_condominium_the_caller_does_not_manage_is_forbidden()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/condominiums/{_otherCondominiumId}/blocks");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<HttpResponseMessage> CreateAsync(
        Guid callerId,
        string? identifier) =>
        _host.ClientFor(callerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/blocks", new { identifier });

    private async Task<Guid> CreatedBlockIdAsync(string identifier)
    {
        var response = await CreateAsync(_managerId, identifier);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CondominiumBlockEndpoints.Response>();
        return body!.Id;
    }

    private Task<Guid> AddUnitAsync(Guid blockId, string identifier) =>
        _host.WithDbAsync(async db =>
        {
            var unit = new Unit(_condominiumId, identifier, blockId, null, null);
            db.Units.Add(unit);
            await db.SaveChangesAsync();
            return unit.Id;
        });

    private Task<int> BlockCountAsync() =>
        _host.WithDbAsync(db => db.CondominiumBlocks
            .CountAsync(block => block.CondominiumId == _condominiumId));

    private sealed record ErrorResponse(string Error);
}
