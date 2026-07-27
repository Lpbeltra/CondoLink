using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.UnitMemberships;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

/// <summary>
/// Endpoint-level guarantees for linking people to units: only a manager of the
/// condominium that owns the unit may act, the target user must already be an
/// active member of that same condominium, and a relationship cannot be
/// duplicated.
/// </summary>
public sealed class UnitMembershipEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;

    private Guid _condominiumId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _residentId;
    private Guid _secondResidentId;
    private Guid _outsiderId;
    private Guid _unitId;
    private Guid _foreignUnitId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(application =>
        {
            application.MapCreateUnitMembership();
            application.MapManageUnitMembership();
            application.MapListUnitMemberships();
        });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var otherCondominium = new Condominium("Residencial Beta", null, null);
            var unit = new Unit(condominium.Id, "101", null, null, null);
            var foreignUnit = new Unit(otherCondominium.Id, "999", null, null, null);
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var otherManager = CoreTestSeed.User("Sindico Beta", "beta@example.com");
            var resident = CoreTestSeed.User("Morador", "morador@example.com");
            var secondResident = CoreTestSeed.User("Vizinho", "vizinho@example.com");
            var outsider = CoreTestSeed.User("Estranho", "estranho@example.com");

            db.AddRange(
                condominium, otherCondominium, unit, foreignUnit, manager,
                otherManager, resident, secondResident, outsider);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, otherManager.Id, otherCondominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, resident.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(
                db, secondResident.Id, condominium.Id, CondominiumRole.Resident);
            CoreTestSeed.AddMember(
                db, outsider.Id, otherCondominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();

            _condominiumId = condominium.Id;
            _managerId = manager.Id;
            _otherManagerId = otherManager.Id;
            _residentId = resident.Id;
            _secondResidentId = secondResident.Id;
            _outsiderId = outsider.Id;
            _unitId = unit.Id;
            _foreignUnitId = foreignUnit.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Manager_can_link_a_member_to_a_unit_and_receives_201()
    {
        var response = await LinkAsync(_managerId, _residentId, "Owner", true, true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CreateUnitMembership.Response>();
        Assert.Equal(_residentId, body!.UserId);
        Assert.Equal(_unitId, body.UnitId);
        Assert.Equal("Owner", body.RelationshipType);
        Assert.True(body.IsResident);
        Assert.True(body.IsPrimaryResidence);
        Assert.True(body.IsActive);
        Assert.Null(body.EndedAt);
    }

    [Fact]
    public async Task Resident_cannot_link_people_to_a_unit()
    {
        var response = await LinkAsync(_residentId, _residentId, "Owner");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await MembershipCountAsync());
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_link_people_to_this_unit()
    {
        var response = await LinkAsync(_otherManagerId, _residentId, "Owner");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await MembershipCountAsync());
    }

    [Fact]
    public async Task Anonymous_caller_cannot_link_people_to_a_unit()
    {
        var response = await _host.AnonymousClient().PostAsJsonAsync(
            $"/units/{_unitId}/memberships",
            new
            {
                userId = _residentId,
                relationshipType = "Owner",
                isResident = true,
                isPrimaryResidence = false
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_user_who_is_not_a_member_of_the_condominium_cannot_be_linked()
    {
        var response = await LinkAsync(_managerId, _outsiderId, "Owner");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(0, await MembershipCountAsync());
    }

    [Fact]
    public async Task Linking_a_missing_user_returns_404()
    {
        var response = await LinkAsync(_managerId, Guid.NewGuid(), "Owner");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Linking_to_a_missing_unit_returns_404()
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/units/{Guid.NewGuid()}/memberships",
            new
            {
                userId = _residentId,
                relationshipType = "Owner",
                isResident = true,
                isPrimaryResidence = false
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_link_a_member_to_a_unit_of_another_condominium()
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/units/{_foreignUnitId}/memberships",
            new
            {
                userId = _residentId,
                relationshipType = "Owner",
                isResident = true,
                isPrimaryResidence = false
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_active_relationship_returns_409()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(_managerId, _residentId, "Owner")).StatusCode);

        var response = await LinkAsync(_managerId, _residentId, "owner");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await MembershipCountAsync());
    }

    [Fact]
    public async Task A_second_relationship_type_for_the_same_user_and_unit_is_allowed()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await LinkAsync(_managerId, _residentId, "Owner")).StatusCode);

        var response = await LinkAsync(_managerId, _residentId, "Tenant");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2, await MembershipCountAsync());
    }

    [Fact]
    public async Task Relinking_an_ended_relationship_reactivates_it_instead_of_duplicating()
    {
        var membershipId = await LinkedMembershipIdAsync(_residentId, "Owner");
        Assert.Equal(HttpStatusCode.NoContent,
            (await _host.ClientFor(_managerId).DeleteAsync(
                $"/units/{_unitId}/memberships/{membershipId}")).StatusCode);

        var response = await LinkAsync(_managerId, _residentId, "Owner", true, true);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await MembershipCountAsync());
        var membership = await _host.WithDbAsync(db => db.UnitMemberships
            .AsNoTracking().SingleAsync(item => item.Id == membershipId));
        Assert.True(membership.IsActive);
        Assert.Null(membership.EndedAt);
        Assert.True(membership.IsPrimaryResidence);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Proprietario")]
    [InlineData("1")]
    public async Task Unparseable_relationship_type_returns_400(string? relationshipType)
    {
        var response = await LinkAsync(_managerId, _residentId, relationshipType);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Primary_residence_without_resident_flag_returns_400()
    {
        var response = await LinkAsync(
            _managerId, _residentId, "Owner",
            isResident: false, isPrimaryResidence: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await MembershipCountAsync());
    }

    [Fact]
    public async Task Inactive_unit_cannot_receive_new_memberships()
    {
        // Unit exposes no deactivation method, so the flag is flipped through
        // the store directly.
        await _host.WithDbAsync<int>(db => db.Units
            .Where(unit => unit.Id == _unitId)
            .ExecuteUpdateAsync(setters =>
                setters.SetProperty(unit => unit.IsActive, false)));

        var response = await LinkAsync(_managerId, _residentId, "Owner");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_condominium_cannot_receive_new_unit_memberships()
    {
        await _host.WithDbAsync(async db =>
        {
            var condominium = await db.Condominiums
                .SingleAsync(item => item.Id == _condominiumId);
            condominium.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await LinkAsync(_managerId, _residentId, "Owner");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_change_the_relationship_of_a_link()
    {
        var membershipId = await LinkedMembershipIdAsync(_residentId, "Owner");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/units/{_unitId}/memberships/{membershipId}",
            new
            {
                relationshipType = "Tenant",
                isResident = true,
                isPrimaryResidence = false
            });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var membership = await _host.WithDbAsync(db => db.UnitMemberships
            .AsNoTracking().SingleAsync(item => item.Id == membershipId));
        Assert.Equal(UnitRelationshipType.Tenant, membership.RelationshipType);
        Assert.False(membership.IsPrimaryResidence);
    }

    [Fact]
    public async Task Changing_a_link_onto_a_relationship_the_user_already_has_returns_409()
    {
        var ownerId = await LinkedMembershipIdAsync(_residentId, "Owner");
        await LinkedMembershipIdAsync(_residentId, "Tenant");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/units/{_unitId}/memberships/{ownerId}",
            new
            {
                relationshipType = "Tenant",
                isResident = true,
                isPrimaryResidence = false
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Resident_cannot_change_or_end_a_link()
    {
        var membershipId = await LinkedMembershipIdAsync(_residentId, "Owner");
        var resident = _host.ClientFor(_residentId);

        var update = await resident.PutAsJsonAsync(
            $"/units/{_unitId}/memberships/{membershipId}",
            new
            {
                relationshipType = "Tenant",
                isResident = true,
                isPrimaryResidence = false
            });
        var delete = await resident.DeleteAsync(
            $"/units/{_unitId}/memberships/{membershipId}");

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.True(await _host.WithDbAsync(db => db.UnitMemberships
            .AsNoTracking().Where(item => item.Id == membershipId)
            .Select(item => item.IsActive).SingleAsync()));
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_change_a_link()
    {
        var membershipId = await LinkedMembershipIdAsync(_residentId, "Owner");

        var response = await _host.ClientFor(_otherManagerId).PutAsJsonAsync(
            $"/units/{_unitId}/memberships/{membershipId}",
            new
            {
                relationshipType = "Tenant",
                isResident = true,
                isPrimaryResidence = false
            });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Changing_a_link_that_belongs_to_a_different_unit_returns_404()
    {
        var membershipId = await LinkedMembershipIdAsync(_residentId, "Owner");

        var response = await _host.ClientFor(_managerId).PutAsJsonAsync(
            $"/units/{_foreignUnitId}/memberships/{membershipId}",
            new
            {
                relationshipType = "Tenant",
                isResident = true,
                isPrimaryResidence = false
            });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Manager_can_end_a_link_and_the_row_is_kept_as_inactive()
    {
        var membershipId = await LinkedMembershipIdAsync(_residentId, "Owner");

        var response = await _host.ClientFor(_managerId).DeleteAsync(
            $"/units/{_unitId}/memberships/{membershipId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var membership = await _host.WithDbAsync(db => db.UnitMemberships
            .AsNoTracking().SingleAsync(item => item.Id == membershipId));
        Assert.False(membership.IsActive);
        Assert.NotNull(membership.EndedAt);
    }

    [Fact]
    public async Task Ending_an_already_inactive_link_returns_409()
    {
        var membershipId = await LinkedMembershipIdAsync(_residentId, "Owner");
        var manager = _host.ClientFor(_managerId);
        await manager.DeleteAsync($"/units/{_unitId}/memberships/{membershipId}");

        var response = await manager.DeleteAsync(
            $"/units/{_unitId}/memberships/{membershipId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Editing_an_inactive_link_returns_409()
    {
        var membershipId = await LinkedMembershipIdAsync(_residentId, "Owner");
        var manager = _host.ClientFor(_managerId);
        await manager.DeleteAsync($"/units/{_unitId}/memberships/{membershipId}");

        var response = await manager.PutAsJsonAsync(
            $"/units/{_unitId}/memberships/{membershipId}",
            new
            {
                relationshipType = "Owner",
                isResident = true,
                isPrimaryResidence = false
            });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Manager_lists_the_links_of_a_unit_with_active_ones_first()
    {
        var endedId = await LinkedMembershipIdAsync(_residentId, "Owner");
        await _host.ClientFor(_managerId).DeleteAsync(
            $"/units/{_unitId}/memberships/{endedId}");
        await LinkedMembershipIdAsync(_secondResidentId, "Tenant");

        var memberships = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<List<ListUnitMemberships.Response>>(
                $"/units/{_unitId}/memberships") ?? [];

        Assert.Equal(2, memberships.Count);
        Assert.True(memberships[0].MembershipActive);
        Assert.Equal(_secondResidentId, memberships[0].UserId);
        Assert.Equal("Vizinho", memberships[0].FullName);
        Assert.False(memberships[1].MembershipActive);
        Assert.Equal(endedId, memberships[1].UnitMembershipId);
    }

    [Fact]
    public async Task Resident_cannot_list_the_links_of_a_unit()
    {
        var response = await _host.ClientFor(_residentId)
            .GetAsync($"/units/{_unitId}/memberships");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_list_the_links_of_this_unit()
    {
        var response = await _host.ClientFor(_otherManagerId)
            .GetAsync($"/units/{_unitId}/memberships");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listing_the_links_of_a_missing_unit_returns_404()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/units/{Guid.NewGuid()}/memberships");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> LinkAsync(
        Guid callerId,
        Guid userId,
        string? relationshipType,
        bool isResident = true,
        bool isPrimaryResidence = false) =>
        _host.ClientFor(callerId).PostAsJsonAsync(
            $"/units/{_unitId}/memberships",
            new { userId, relationshipType, isResident, isPrimaryResidence });

    private async Task<Guid> LinkedMembershipIdAsync(
        Guid userId,
        string relationshipType)
    {
        var response = await LinkAsync(_managerId, userId, relationshipType);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<CreateUnitMembership.Response>();
        return body!.Id;
    }

    private Task<int> MembershipCountAsync() =>
        _host.WithDbAsync(db => db.UnitMemberships
            .CountAsync(membership => membership.UnitId == _unitId));
}
