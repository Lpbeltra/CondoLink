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
/// Endpoint-level guarantees for PATCH /requests/{id}/status and
/// PATCH /requests/{id}/priority: only a manager of the owning condominium may
/// move a request, the domain transition matrix is honoured, and every accepted
/// status change leaves an audit row with the correct previous/new status.
/// </summary>
public sealed class RequestStatusEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;

    private Guid _condominiumId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _residentId;
    private Guid _requestId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(application =>
        {
            application.MapUpdateRequestStatus();
            application.MapUpdateRequestPriority();
        });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var otherCondominium = new Condominium("Residencial Beta", null, null);
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var otherManager = CoreTestSeed.User("Sindico Beta", "beta@example.com");
            var resident = CoreTestSeed.User("Morador", "morador@example.com");
            var category = new Category(condominium.Id, "Manutenção", null);
            var request = new DomainRequest(
                condominium.Id, resident.Id, null, category.Id,
                "Vazamento", "Água no corredor");

            db.AddRange(
                condominium, otherCondominium, manager, otherManager,
                resident, category, request);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, otherManager.Id, otherCondominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, resident.Id, condominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();

            _condominiumId = condominium.Id;
            _managerId = manager.Id;
            _otherManagerId = otherManager.Id;
            _residentId = resident.Id;
            _requestId = request.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Anonymous_caller_cannot_update_request_status()
    {
        var response = await _host.AnonymousClient().PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(RequestStatus.Open, await CurrentStatusAsync());
    }

    [Fact]
    public async Task Resident_cannot_update_request_status()
    {
        var response = await _host.ClientFor(_residentId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(RequestStatus.Open, await CurrentStatusAsync());
        Assert.Empty(await HistoryAsync());
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_update_request_status()
    {
        var response = await _host.ClientFor(_otherManagerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(RequestStatus.Open, await CurrentStatusAsync());
    }

    [Fact]
    public async Task Manager_with_a_revoked_role_cannot_update_request_status()
    {
        await _host.WithDbAsync(async db =>
        {
            var membership = await db.CondominiumMemberships
                .SingleAsync(item =>
                    item.UserId == _managerId
                    && item.CondominiumId == _condominiumId);
            var role = await db.CondominiumMembershipRoles
                .SingleAsync(item =>
                    item.CondominiumMembershipId == membership.Id
                    && item.Role == CondominiumRole.Manager);
            role.Deactivate();
            await db.SaveChangesAsync();
        });

        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_cannot_update_request_status()
    {
        await _host.WithDbAsync(async db =>
        {
            var manager = await db.Set<ApplicationUser>()
                .SingleAsync(user => user.Id == _managerId);
            manager.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Caller_without_a_user_row_is_unauthorized()
    {
        var response = await _host.ClientFor(Guid.NewGuid()).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_move_an_open_request_to_in_progress()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<UpdateRequestStatus.Response>();
        Assert.Equal("InProgress", body!.Status);
        Assert.Null(body.ResolvedAt);
        Assert.Equal(RequestStatus.InProgress, await CurrentStatusAsync());
    }

    [Fact]
    public async Task Accepted_status_change_writes_a_history_row_with_previous_and_new_status()
    {
        var manager = _host.ClientFor(_managerId);

        await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status",
            new { status = "InProgress", reason = "  Equipe acionada  " });
        await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "Resolved" });

        var history = await HistoryAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(RequestStatus.Open, history[0].PreviousStatus);
        Assert.Equal(RequestStatus.InProgress, history[0].NewStatus);
        Assert.Equal("Equipe acionada", history[0].Reason);
        Assert.Equal(_managerId, history[0].ChangedByUserId);
        Assert.Equal(RequestStatus.InProgress, history[1].PreviousStatus);
        Assert.Equal(RequestStatus.Resolved, history[1].NewStatus);
        Assert.Null(history[1].Reason);
    }

    [Fact]
    public async Task Resolving_a_request_stamps_resolved_at_and_reopening_clears_it()
    {
        var manager = _host.ClientFor(_managerId);

        var resolved = await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "Resolved" });
        var resolvedBody = await resolved.Content
            .ReadFromJsonAsync<UpdateRequestStatus.Response>();
        Assert.NotNull(resolvedBody!.ResolvedAt);

        var reopened = await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "Open" });
        var reopenedBody = await reopened.Content
            .ReadFromJsonAsync<UpdateRequestStatus.Response>();
        Assert.Equal(HttpStatusCode.OK, reopened.StatusCode);
        Assert.Null(reopenedBody!.ResolvedAt);
    }

    [Fact]
    public async Task Setting_the_status_a_request_already_has_returns_409()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "Open" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Empty(await HistoryAsync());
    }

    [Fact]
    public async Task Status_comparison_is_case_insensitive_when_detecting_the_same_status()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "open" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("WaitingForResident")]
    [InlineData("WaitingForThirdParty")]
    public async Task Transition_forbidden_by_the_matrix_returns_409_and_leaves_no_history(
        string target)
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = target });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(RequestStatus.Open, await CurrentStatusAsync());
        Assert.Empty(await HistoryAsync());
    }

    [Fact]
    public async Task Cancelled_request_can_only_be_reopened()
    {
        var manager = _host.ClientFor(_managerId);
        Assert.Equal(HttpStatusCode.OK, (await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status",
            new { status = "Cancelled" })).StatusCode);

        var toInProgress = await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "InProgress" });
        Assert.Equal(HttpStatusCode.Conflict, toInProgress.StatusCode);

        var toOpen = await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "Open" });
        Assert.Equal(HttpStatusCode.OK, toOpen.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Frozen")]
    [InlineData("2")]
    public async Task Unparseable_status_returns_400(string? status)
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RequestStatus.Open, await CurrentStatusAsync());
    }

    [Fact]
    public async Task Reason_of_exactly_500_characters_is_accepted()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status",
            new { status = "InProgress", reason = new string('a', 500) });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var history = Assert.Single(await HistoryAsync());
        Assert.Equal(500, history.Reason!.Length);
    }

    [Fact]
    public async Task Reason_longer_than_500_characters_returns_400()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status",
            new { status = "InProgress", reason = new string('a', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(RequestStatus.Open, await CurrentStatusAsync());
        Assert.Empty(await HistoryAsync());
    }

    [Fact]
    public async Task Whitespace_only_reason_is_stored_as_null()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/status",
            new { status = "InProgress", reason = "    " });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Assert.Single(await HistoryAsync()).Reason);
    }

    [Fact]
    public async Task Updating_the_status_of_a_missing_request_returns_404()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{Guid.NewGuid()}/status", new { status = "InProgress" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Resident_cannot_update_request_priority()
    {
        var response = await _host.ClientFor(_residentId).PatchAsJsonAsync(
            $"/requests/{_requestId}/priority", new { priority = "High" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            RequestPriority.Normal,
            await _host.WithDbAsync(db => db.Requests
                .Where(item => item.Id == _requestId)
                .Select(item => item.Priority)
                .SingleAsync()));
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_update_request_priority()
    {
        var response = await _host.ClientFor(_otherManagerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/priority", new { priority = "High" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_raise_the_priority()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/priority", new { priority = "High" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<UpdateRequestPriority.Response>();
        Assert.Equal("High", body!.Priority);
    }

    [Fact]
    public async Task Setting_the_priority_a_request_already_has_returns_409()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/priority", new { priority = "Normal" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Cancelled_request_cannot_have_its_priority_changed()
    {
        var manager = _host.ClientFor(_managerId);
        await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/status", new { status = "Cancelled" });

        var response = await manager.PatchAsJsonAsync(
            $"/requests/{_requestId}/priority", new { priority = "High" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Urgente")]
    [InlineData("3")]
    public async Task Unparseable_priority_returns_400(string? priority)
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/priority", new { priority });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Updating_the_priority_of_a_missing_request_returns_404()
    {
        var response = await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{Guid.NewGuid()}/priority", new { priority = "High" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Priority_changes_never_write_status_history()
    {
        await _host.ClientFor(_managerId).PatchAsJsonAsync(
            $"/requests/{_requestId}/priority", new { priority = "High" });

        Assert.Empty(await HistoryAsync());
    }

    private Task<RequestStatus> CurrentStatusAsync() =>
        _host.WithDbAsync(db => db.Requests
            .AsNoTracking()
            .Where(item => item.Id == _requestId)
            .Select(item => item.Status)
            .SingleAsync());

    private Task<List<RequestStatusHistory>> HistoryAsync() =>
        _host.WithDbAsync(db => db.RequestStatusHistories
            .AsNoTracking()
            .Where(item => item.RequestId == _requestId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .ToListAsync());
}
