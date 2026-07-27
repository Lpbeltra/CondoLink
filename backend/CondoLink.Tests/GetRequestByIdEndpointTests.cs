using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Tests;

/// <summary>
/// GET /requests/{id} is readable by the author and by managers of the owning
/// condominium only. Everyone else — including a manager of a different
/// condominium and a plain co-resident — must be refused.
/// </summary>
public sealed class GetRequestByIdEndpointTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;

    private Guid _authorId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _coResidentId;
    private Guid _outsiderId;
    private Guid _requestId;
    private Guid _unitId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(application =>
        {
            application.MapGetRequestById();
            application.MapUpdateRequestStatus();
        });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var otherCondominium = new Condominium("Residencial Beta", null, null);
            var block = new CondominiumBlock(condominium.Id, "Torre A");
            var unit = new Unit(condominium.Id, "101", block.Id, "1", null);
            var author = CoreTestSeed.User("Autor", "autor@example.com");
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var otherManager = CoreTestSeed.User("Sindico Beta", "beta@example.com");
            var coResident = CoreTestSeed.User("Vizinho", "vizinho@example.com");
            var outsider = CoreTestSeed.User("Estranho", "estranho@example.com");
            var category = new Category(condominium.Id, "Manutenção", null);
            var request = new DomainRequest(
                condominium.Id, author.Id, unit.Id, category.Id,
                "Vazamento", "Água no corredor");

            db.AddRange(
                condominium, otherCondominium, block, unit, author, manager,
                otherManager, coResident, outsider, category, request);
            CoreTestSeed.AddMember(
                db, author.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, coResident.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(
                db, otherManager.Id, otherCondominium.Id, CondominiumRole.Manager);
            await db.SaveChangesAsync();

            _authorId = author.Id;
            _managerId = manager.Id;
            _otherManagerId = otherManager.Id;
            _coResidentId = coResident.Id;
            _outsiderId = outsider.Id;
            _requestId = request.Id;
            _unitId = unit.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Author_can_read_their_own_request()
    {
        var response = await _host.ClientFor(_authorId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<GetRequestById.Response>();
        Assert.Equal(_requestId, body!.Id);
        Assert.Equal(_authorId, body.Author.Id);
        Assert.Equal("Autor", body.Author.FullName);
        Assert.Equal("Manutenção", body.Category.Name);
        Assert.Equal("Open", body.Status);
    }

    [Fact]
    public async Task Request_details_include_the_target_unit_with_its_block()
    {
        var body = await _host.ClientFor(_authorId)
            .GetFromJsonAsync<GetRequestById.Response>(
                $"/requests/{_requestId}");

        Assert.NotNull(body!.TargetUnit);
        Assert.Equal(_unitId, body.TargetUnit.Id);
        Assert.Equal("101", body.TargetUnit.Identifier);
        Assert.Equal("Torre A", body.TargetUnit.Block);
    }

    [Fact]
    public async Task Manager_of_the_owning_condominium_can_read_the_request()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Manager_of_another_condominium_is_forbidden()
    {
        var response = await _host.ClientFor(_otherManagerId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Co_resident_who_is_not_the_author_is_forbidden()
    {
        var response = await _host.ClientFor(_coResidentId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Non_member_of_the_condominium_is_forbidden()
    {
        var response = await _host.ClientFor(_outsiderId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_caller_cannot_read_a_request()
    {
        var response = await _host.AnonymousClient()
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_author_is_forbidden_from_reading_their_own_request()
    {
        await _host.WithDbAsync(async db =>
        {
            var author = await db.Set<ApplicationUser>()
                .SingleAsync(user => user.Id == _authorId);
            author.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await _host.ClientFor(_authorId)
            .GetAsync($"/requests/{_requestId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Missing_request_returns_404()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/requests/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Status_history_is_exposed_in_chronological_order_with_the_actor_name()
    {
        var manager = _host.ClientFor(_managerId);
        await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status",
            new { status = "InProgress", reason = "Equipe acionada" });
        await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "Resolved" });

        var body = await _host.ClientFor(_authorId)
            .GetFromJsonAsync<GetRequestById.Response>(
                $"/requests/{_requestId}");

        Assert.Equal(2, body!.StatusHistory.Count);
        Assert.Equal("Open", body.StatusHistory[0].PreviousStatus);
        Assert.Equal("InProgress", body.StatusHistory[0].NewStatus);
        Assert.Equal("Equipe acionada", body.StatusHistory[0].Reason);
        Assert.Equal("Sindico Alfa", body.StatusHistory[0].ChangedByFullName);
        Assert.Equal("InProgress", body.StatusHistory[1].PreviousStatus);
        Assert.Equal("Resolved", body.StatusHistory[1].NewStatus);
    }
}
