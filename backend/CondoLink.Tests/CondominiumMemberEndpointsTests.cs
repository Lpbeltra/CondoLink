using System.Net;
using System.Net.Http.Json;
using CondoLink.Api.Features.CondominiumMemberRoles;
using CondoLink.Api.Features.CondominiumMembers;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CondoLink.Tests;

/// <summary>
/// Endpoint-level guarantees for the condominium roster: adding members,
/// granting roles, onboarding (existing-user and brand-new-user paths) and
/// listing the roster are all restricted to managers of that condominium.
/// </summary>
public sealed class CondominiumMemberEndpointsTests : IAsyncLifetime
{
    private CoreEndpointTestHost _host = null!;

    private Guid _condominiumId;
    private Guid _managerId;
    private Guid _otherManagerId;
    private Guid _residentId;
    private Guid _unlinkedUserId;
    private Guid _unitId;
    private Guid _secondUnitId;
    private Guid _foreignUnitId;
    private Guid _residentMembershipId;

    public async Task InitializeAsync()
    {
        _host = await CoreEndpointTestHost.StartAsync(application =>
        {
            application.MapAddCondominiumMember();
            application.MapAddCondominiumMemberRole();
            application.MapOnboardCondominiumMember();
            application.MapListCondominiumMembers();
            application.MapUpdateCondominiumMember();
        });

        await _host.WithDbAsync(async db =>
        {
            var condominium = new Condominium("Residencial Alfa", null, null);
            var otherCondominium = new Condominium("Residencial Beta", null, null);
            var unit = new Unit(condominium.Id, "101", null, null, null);
            var secondUnit = new Unit(condominium.Id, "102", null, null, null);
            var foreignUnit = new Unit(otherCondominium.Id, "999", null, null, null);
            var manager = CoreTestSeed.User("Sindico Alfa", "alfa@example.com");
            var otherManager = CoreTestSeed.User("Sindico Beta", "beta@example.com");
            var resident = CoreTestSeed.User("Morador", "morador@example.com");
            var unlinked = CoreTestSeed.User("Sem Vinculo", "sem@example.com");

            db.AddRange(
                condominium, otherCondominium, unit, secondUnit, foreignUnit,
                manager, otherManager, resident, unlinked);
            CoreTestSeed.AddMember(
                db, manager.Id, condominium.Id, CondominiumRole.Manager);
            CoreTestSeed.AddMember(
                db, otherManager.Id, otherCondominium.Id, CondominiumRole.Manager);
            var residentMembership = CoreTestSeed.AddMember(
                db, resident.Id, condominium.Id, CondominiumRole.Resident);
            await db.SaveChangesAsync();

            _condominiumId = condominium.Id;
            _managerId = manager.Id;
            _otherManagerId = otherManager.Id;
            _residentId = resident.Id;
            _unlinkedUserId = unlinked.Id;
            _unitId = unit.Id;
            _secondUnitId = secondUnit.Id;
            _foreignUnitId = foreignUnit.Id;
            _residentMembershipId = residentMembership.Id;
        });
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Manager_can_update_profile_and_create_unit_link()
    {
        var response = await UpdateMemberAsync(
            _managerId, _residentId, null, _unitId,
            fullName: "Maria Atualizada",
            email: "maria.atualizada@example.com");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<UpdateCondominiumMember.Response>();
        Assert.Equal("Maria Atualizada", body!.FullName);
        Assert.Equal("maria.atualizada@example.com", body.Email);
        Assert.Equal(_unitId, body.UnitLink!.UnitId);
        Assert.True(await _host.WithDbAsync(db => db.UnitMemberships
            .AnyAsync(item =>
                item.UserId == _residentId
                && item.UnitId == _unitId
                && item.IsActive)));
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_edit_person()
    {
        var response = await UpdateMemberAsync(
            _otherManagerId, _residentId, null, _unitId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Duplicate_email_is_rejected()
    {
        var response = await UpdateMemberAsync(
            _managerId, _residentId, null, null,
            email: "alfa@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Moving_unit_ends_previous_link_without_deleting_history()
    {
        var previousLinkId = await _host.WithDbAsync(async db =>
        {
            var link = new UnitMembership(
                _residentId, _unitId, UnitRelationshipType.Owner, true, true);
            db.UnitMemberships.Add(link);
            await db.SaveChangesAsync();
            return link.Id;
        });

        var response = await UpdateMemberAsync(
            _managerId, _residentId, previousLinkId, _secondUnitId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var links = await _host.WithDbAsync(db => db.UnitMemberships
            .Where(item => item.UserId == _residentId)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync());
        Assert.Equal(2, links.Count);
        Assert.False(links.Single(item => item.Id == previousLinkId).IsActive);
        Assert.NotNull(links.Single(item => item.Id == previousLinkId).EndedAt);
        Assert.True(links.Single(item => item.UnitId == _secondUnitId).IsActive);
    }

    [Fact]
    public async Task Manager_can_add_an_existing_user_as_a_member()
    {
        var response = await AddMemberAsync(_managerId, _unlinkedUserId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<AddCondominiumMember.Response>();
        Assert.Equal(_unlinkedUserId, body!.UserId);
        Assert.Equal(_condominiumId, body.CondominiumId);
        Assert.True(body.IsActive);
        Assert.Null(body.EndedAt);
    }

    [Fact]
    public async Task Resident_cannot_add_a_member()
    {
        var response = await AddMemberAsync(_residentId, _unlinkedUserId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await IsMemberAsync(_unlinkedUserId, _condominiumId));
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_add_a_member_here()
    {
        var response = await AddMemberAsync(_otherManagerId, _unlinkedUserId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await IsMemberAsync(_unlinkedUserId, _condominiumId));
    }

    [Fact]
    public async Task Anonymous_caller_cannot_add_a_member()
    {
        var response = await _host.AnonymousClient().PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/members",
            new { userId = _unlinkedUserId });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Adding_the_same_user_twice_returns_409()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await AddMemberAsync(_managerId, _unlinkedUserId)).StatusCode);

        var response = await AddMemberAsync(_managerId, _unlinkedUserId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Adding_a_missing_user_returns_404()
    {
        var response = await AddMemberAsync(_managerId, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Adding_a_member_with_an_empty_user_id_returns_400()
    {
        var response = await AddMemberAsync(_managerId, Guid.Empty);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_condominium_cannot_receive_new_members()
    {
        await SetCondominiumActiveAsync(false);

        var response = await AddMemberAsync(_managerId, _unlinkedUserId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_cannot_be_added_as_a_member()
    {
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Set<ApplicationUser>()
                .SingleAsync(item => item.Id == _unlinkedUserId);
            user.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await AddMemberAsync(_managerId, _unlinkedUserId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Granting_a_second_manager_role_is_rejected()
    {
        // A condominium may only have one síndico, so promoting a second member
        // is refused even though the membership itself is valid.
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominium-memberships/{_residentMembershipId}/roles",
            new { role = "manager" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(await _host.WithDbAsync(db => db.CondominiumMembershipRoles
            .AnyAsync(role =>
                role.CondominiumMembershipId == _residentMembershipId
                && role.Role == CondominiumRole.Manager)));
    }

    [Fact]
    public async Task Resident_cannot_grant_roles()
    {
        var response = await _host.ClientFor(_residentId).PostAsJsonAsync(
            $"/condominium-memberships/{_residentMembershipId}/roles",
            new { role = "Manager" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_grant_roles_here()
    {
        var response = await _host.ClientFor(_otherManagerId).PostAsJsonAsync(
            $"/condominium-memberships/{_residentMembershipId}/roles",
            new { role = "Manager" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Granting_a_role_the_membership_already_has_returns_409()
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominium-memberships/{_residentMembershipId}/roles",
            new { role = "Resident" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Porteiro")]
    [InlineData("1")]
    public async Task Unparseable_role_returns_400(string? role)
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominium-memberships/{_residentMembershipId}/roles",
            new { role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Granting_a_role_on_a_missing_membership_returns_404()
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominium-memberships/{Guid.NewGuid()}/roles",
            new { role = "Manager" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Granting_a_role_in_an_inactive_condominium_returns_409()
    {
        await SetCondominiumActiveAsync(false);

        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominium-memberships/{_residentMembershipId}/roles",
            new { role = "Manager" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Onboarding_a_brand_new_user_creates_the_account_membership_and_resident_role()
    {
        var response = await OnboardAsync(
            _managerId, "  Novo Morador  ", "  NOVO@EXAMPLE.COM  ", " 11999 ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<OnboardCondominiumMember.Response>();
        Assert.True(body!.IsNewUser);
        Assert.False(string.IsNullOrWhiteSpace(body.InitialPassword));
        Assert.Equal("Novo Morador", body.User.FullName);
        Assert.Equal("novo@example.com", body.User.Email);
        Assert.Equal("11999", body.User.PhoneNumber);
        Assert.Equal(["Resident"], body.Roles);
        Assert.Null(body.UnitMembership);
        Assert.Equal(_condominiumId, body.Membership.CondominiumId);
        Assert.True(await IsMemberAsync(body.User.Id, _condominiumId));
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Set<ApplicationUser>()
                .SingleAsync(item => item.Id == body.User.Id);
            Assert.True(user.MustChangePassword);
            Assert.Null(user.LastLoginAt);
            Assert.Null(user.PasswordChangedAt);
        });
    }

    [Fact]
    public async Task Onboarding_a_new_user_issues_a_password_that_satisfies_the_identity_policy()
    {
        var response = await OnboardAsync(
            _managerId, "Novo Morador", "novo@example.com");
        var body = await response.Content
            .ReadFromJsonAsync<OnboardCondominiumMember.Response>();

        var passwordIsValid = false;
        await _host.WithServicesAsync(async services =>
        {
            var userManager = services
                .GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByIdAsync(
                body!.User.Id.ToString());
            passwordIsValid = await userManager.CheckPasswordAsync(
                user!, body.InitialPassword!);
        });

        Assert.True(passwordIsValid,
            "The generated initial password must authenticate the new user.");
    }

    [Fact]
    public async Task Onboarding_an_existing_user_reuses_the_account_and_returns_no_password()
    {
        var response = await OnboardAsync(
            _managerId, "Nome Ignorado", "sem@example.com");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<OnboardCondominiumMember.Response>();
        Assert.False(body!.IsNewUser);
        Assert.Null(body.InitialPassword);
        Assert.Equal(_unlinkedUserId, body.User.Id);
        Assert.Equal("Sem Vinculo", body.User.FullName);
        Assert.True(await IsMemberAsync(_unlinkedUserId, _condominiumId));
    }

    [Fact]
    public async Task Onboarding_an_existing_member_again_is_idempotent()
    {
        var response = await OnboardAsync(
            _managerId, "Morador", "morador@example.com");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<OnboardCondominiumMember.Response>();
        Assert.False(body!.IsNewUser);
        Assert.Equal(_residentMembershipId, body.Membership.Id);
        Assert.Equal(1, await _host.WithDbAsync(db => db.CondominiumMemberships
            .CountAsync(membership =>
                membership.UserId == _residentId
                && membership.CondominiumId == _condominiumId)));
    }

    [Fact]
    public async Task Onboarding_with_a_target_unit_also_creates_the_unit_link()
    {
        var response = await OnboardAsync(
            _managerId, "Novo Morador", "novo@example.com",
            unitId: _unitId, relationshipType: "Owner",
            isResident: true, isPrimaryResidence: true);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content
            .ReadFromJsonAsync<OnboardCondominiumMember.Response>();
        Assert.NotNull(body!.UnitMembership);
        Assert.Equal(_unitId, body.UnitMembership.UnitId);
        Assert.Equal("Owner", body.UnitMembership.RelationshipType);
        Assert.True(body.UnitMembership.IsPrimaryResidence);
        Assert.True(await _host.WithDbAsync(db => db.UnitMemberships
            .AnyAsync(link =>
                link.UserId == body.User.Id && link.UnitId == _unitId)));
    }

    [Fact]
    public async Task Onboarding_onto_a_unit_of_another_condominium_returns_400()
    {
        var response = await OnboardAsync(
            _managerId, "Novo Morador", "novo@example.com",
            unitId: _foreignUnitId, relationshipType: "Owner");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await UserExistsAsync("novo@example.com"));
    }

    [Fact]
    public async Task Onboarding_onto_a_missing_unit_returns_404()
    {
        var response = await OnboardAsync(
            _managerId, "Novo Morador", "novo@example.com",
            unitId: Guid.NewGuid(), relationshipType: "Owner");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unit_relationship_fields_without_a_target_unit_return_400()
    {
        var response = await OnboardAsync(
            _managerId, "Novo Morador", "novo@example.com",
            relationshipType: "Owner");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Onboarding_with_an_unparseable_relationship_returns_400()
    {
        var response = await OnboardAsync(
            _managerId, "Novo Morador", "novo@example.com",
            unitId: _unitId, relationshipType: "Proprietario");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Onboarding_with_primary_residence_but_not_resident_returns_400()
    {
        var response = await OnboardAsync(
            _managerId, "Novo Morador", "novo@example.com",
            unitId: _unitId, relationshipType: "Owner",
            isResident: false, isPrimaryResidence: true);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Onboarding_without_a_full_name_returns_400(string? fullName)
    {
        var response = await OnboardAsync(
            _managerId, fullName, "novo@example.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("nao-e-email")]
    public async Task Onboarding_with_an_invalid_email_returns_400(string? email)
    {
        var response = await OnboardAsync(_managerId, "Novo Morador", email);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resident_cannot_onboard_a_member()
    {
        var response = await OnboardAsync(
            _residentId, "Novo Morador", "novo@example.com");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await UserExistsAsync("novo@example.com"));
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_onboard_here()
    {
        var response = await OnboardAsync(
            _otherManagerId, "Novo Morador", "novo@example.com");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(await UserExistsAsync("novo@example.com"));
    }

    [Fact]
    public async Task Onboarding_into_a_missing_condominium_returns_404()
    {
        var response = await _host.ClientFor(_managerId).PostAsJsonAsync(
            $"/condominiums/{Guid.NewGuid()}/members/onboard",
            new { fullName = "Novo Morador", email = "novo@example.com" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Onboarding_into_an_inactive_condominium_returns_409()
    {
        await SetCondominiumActiveAsync(false);

        var response = await OnboardAsync(
            _managerId, "Novo Morador", "novo@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Onboarding_an_inactive_existing_user_returns_409()
    {
        await _host.WithDbAsync(async db =>
        {
            var user = await db.Set<ApplicationUser>()
                .SingleAsync(item => item.Id == _unlinkedUserId);
            user.SetActiveStatus(false);
            await db.SaveChangesAsync();
        });

        var response = await OnboardAsync(
            _managerId, "Sem Vinculo", "sem@example.com");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Manager_lists_the_roster_with_roles_ordered_by_full_name()
    {
        var members = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<List<ListCondominiumMembers.Response>>(
                $"/condominiums/{_condominiumId}/members") ?? [];

        Assert.Equal(["Morador", "Sindico Alfa"],
            members.Select(member => member.FullName));
        Assert.Equal(["Resident"],
            members.Single(member => member.FullName == "Morador").Roles);
        Assert.Equal(["Manager"],
            members.Single(member => member.FullName == "Sindico Alfa").Roles);
    }

    [Fact]
    public async Task Roster_shows_every_active_role_of_a_membership()
    {
        // Seeded directly rather than via the endpoint: a condominium may only
        // have one síndico, so the API refuses a second Manager. This test is
        // about the roster aggregating multiple roles, not about that rule.
        await _host.WithDbAsync(async db =>
        {
            db.CondominiumMembershipRoles.Add(
                new CondominiumMembershipRole(
                    _residentMembershipId, CondominiumRole.Manager));
            await db.SaveChangesAsync();
            return true;
        });

        var members = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<List<ListCondominiumMembers.Response>>(
                $"/condominiums/{_condominiumId}/members") ?? [];

        Assert.Equal(["Manager", "Resident"],
            members.Single(member => member.FullName == "Morador").Roles);
    }

    [Fact]
    public async Task Roster_omits_revoked_roles()
    {
        await _host.WithDbAsync(async db =>
        {
            var role = await db.CondominiumMembershipRoles.SingleAsync(item =>
                item.CondominiumMembershipId == _residentMembershipId);
            role.Deactivate();
            await db.SaveChangesAsync();
        });

        var members = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<List<ListCondominiumMembers.Response>>(
                $"/condominiums/{_condominiumId}/members") ?? [];

        Assert.Empty(members.Single(member => member.FullName == "Morador").Roles);
    }

    [Fact]
    public async Task Roster_never_leaks_members_of_another_condominium()
    {
        var members = await _host.ClientFor(_managerId)
            .GetFromJsonAsync<List<ListCondominiumMembers.Response>>(
                $"/condominiums/{_condominiumId}/members") ?? [];

        Assert.DoesNotContain(members,
            member => member.FullName == "Sindico Beta");
    }

    [Fact]
    public async Task Resident_cannot_list_the_roster()
    {
        var response = await _host.ClientFor(_residentId)
            .GetAsync($"/condominiums/{_condominiumId}/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_of_another_condominium_cannot_list_this_roster()
    {
        var response = await _host.ClientFor(_otherManagerId)
            .GetAsync($"/condominiums/{_condominiumId}/members");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Listing_the_roster_of_a_missing_condominium_returns_404()
    {
        var response = await _host.ClientFor(_managerId)
            .GetAsync($"/condominiums/{Guid.NewGuid()}/members");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<HttpResponseMessage> AddMemberAsync(Guid callerId, Guid userId) =>
        _host.ClientFor(callerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/members", new { userId });

    private Task<HttpResponseMessage> UpdateMemberAsync(
        Guid callerId,
        Guid userId,
        Guid? unitMembershipId,
        Guid? unitId,
        string fullName = "Morador Atualizado",
        string email = "morador@example.com") =>
        _host.ClientFor(callerId).PutAsJsonAsync(
            $"/condominiums/{_condominiumId}/members/{userId}",
            new
            {
                fullName,
                email,
                phoneNumber = "11999990000",
                cpf = (string?)null,
                cnpj = (string?)null,
                address = "Rua das Flores, 10",
                city = "São Paulo",
                state = "SP",
                membershipActive = true,
                unitMembershipId,
                unitId,
                relationshipType = unitId.HasValue ? "Owner" : null,
                isResident = unitId.HasValue,
                isPrimaryResidence = unitId.HasValue
            });

    private Task<HttpResponseMessage> OnboardAsync(
        Guid callerId,
        string? fullName,
        string? email,
        string? phoneNumber = null,
        Guid? unitId = null,
        string? relationshipType = null,
        bool isResident = false,
        bool isPrimaryResidence = false) =>
        _host.ClientFor(callerId).PostAsJsonAsync(
            $"/condominiums/{_condominiumId}/members/onboard",
            new
            {
                fullName,
                email,
                phoneNumber,
                unitId,
                relationshipType,
                isResident,
                isPrimaryResidence
            });

    private Task SetCondominiumActiveAsync(bool isActive) =>
        _host.WithDbAsync(async db =>
        {
            var condominium = await db.Condominiums
                .SingleAsync(item => item.Id == _condominiumId);
            condominium.SetActiveStatus(isActive);
            await db.SaveChangesAsync();
        });

    private Task<bool> IsMemberAsync(Guid userId, Guid condominiumId) =>
        _host.WithDbAsync(db => db.CondominiumMemberships
            .AnyAsync(membership =>
                membership.UserId == userId
                && membership.CondominiumId == condominiumId));

    private Task<bool> UserExistsAsync(string email) =>
        _host.WithDbAsync(db => db.Set<ApplicationUser>()
            .AnyAsync(user => user.Email == email));
}
