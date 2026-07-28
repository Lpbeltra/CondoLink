using CondoLink.Api.Features.Requests;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

/// <summary>
/// A resident must not be able to attribute a request to a unit they do not
/// occupy; a manager acts for the whole condominium and may.
/// </summary>
public sealed class CreateRequestTargetUnitTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;

    private Condominium _condominium = null!;
    private Unit _ownUnit = null!;
    private Unit _otherUnit = null!;
    private ApplicationUser _resident = null!;
    private ApplicationUser _manager = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        _condominium = new Condominium("Residencial Alfa", null, null);
        _ownUnit = new Unit(_condominium.Id, "101", null, null, null);
        _otherUnit = new Unit(_condominium.Id, "501", null, null, null);
        _resident = User("Morador", "morador@example.com");
        _manager = User("Sindico", "sindico@example.com");

        _db.AddRange(_condominium, _ownUnit, _otherUnit, _resident, _manager);

        var residentMembership = new CondominiumMembership(_resident.Id, _condominium.Id);
        _db.CondominiumMemberships.Add(residentMembership);
        _db.CondominiumMembershipRoles.Add(
            new CondominiumMembershipRole(residentMembership.Id, CondominiumRole.Resident));
        _db.UnitMemberships.Add(
            new UnitMembership(_resident.Id, _ownUnit.Id, UnitRelationshipType.Owner, true, true));

        var managerMembership = new CondominiumMembership(_manager.Id, _condominium.Id);
        _db.CondominiumMemberships.Add(managerMembership);
        _db.CondominiumMembershipRoles.Add(
            new CondominiumMembershipRole(managerMembership.Id, CondominiumRole.Manager));

        await _db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Resident_occupies_only_their_own_unit()
    {
        Assert.True(await OccupiesAsync(_resident.Id, _ownUnit.Id));
        Assert.False(await OccupiesAsync(_resident.Id, _otherUnit.Id));
    }

    [Fact]
    public async Task Automatic_resolution_returns_the_only_active_unit()
    {
        var units = await CreateRequest.ActiveUnitIdsAsync(
            _db, _resident.Id, _condominium.Id);

        Assert.Equal([_ownUnit.Id], units);
    }

    [Fact]
    public async Task Automatic_resolution_detects_multiple_units_and_requires_selection()
    {
        _db.UnitMemberships.Add(new UnitMembership(
            _resident.Id, _otherUnit.Id, UnitRelationshipType.Tenant, true, false));
        await _db.SaveChangesAsync();

        var units = await CreateRequest.ActiveUnitIdsAsync(
            _db, _resident.Id, _condominium.Id);

        Assert.Equal(2, units.Length);
    }

    [Fact]
    public async Task Automatic_resolution_allows_no_unit_without_using_another_condominium()
    {
        var membership = await _db.UnitMemberships.SingleAsync();
        membership.End(DateTime.UtcNow);
        var otherCondominium = new Condominium("Residencial Beta", null, null);
        var foreignUnit = new Unit(
            otherCondominium.Id, "201", null, null, null);
        _db.AddRange(otherCondominium, foreignUnit);
        _db.UnitMemberships.Add(new UnitMembership(
            _resident.Id, foreignUnit.Id, UnitRelationshipType.Owner, true, true));
        await _db.SaveChangesAsync();

        var units = await CreateRequest.ActiveUnitIdsAsync(
            _db, _resident.Id, _condominium.Id);

        Assert.Empty(units);
    }

    [Fact]
    public async Task Resident_is_not_a_manager_so_cannot_target_another_unit()
    {
        var occupies = await OccupiesAsync(_resident.Id, _otherUnit.Id);
        var isManager = await CreateRequest.IsCondominiumManagerAsync(
            _db, _resident.Id, _condominium.Id);

        Assert.False(occupies);
        Assert.False(isManager);
        // Both false => the handler returns 403.
    }

    [Fact]
    public async Task Manager_may_target_a_unit_they_do_not_occupy()
    {
        var occupies = await OccupiesAsync(_manager.Id, _otherUnit.Id);
        var isManager = await CreateRequest.IsCondominiumManagerAsync(
            _db, _manager.Id, _condominium.Id);

        Assert.False(occupies);
        Assert.True(isManager);
        // Manager exemption keeps the handler from returning 403.
    }

    [Fact]
    public async Task Revoked_manager_role_loses_the_exemption()
    {
        var membership = await _db.CondominiumMemberships
            .SingleAsync(item => item.UserId == _manager.Id);
        var role = await _db.CondominiumMembershipRoles
            .SingleAsync(item => item.CondominiumMembershipId == membership.Id);
        role.Deactivate();
        await _db.SaveChangesAsync();

        Assert.False(await CreateRequest.IsCondominiumManagerAsync(
            _db, _manager.Id, _condominium.Id));
    }

    private Task<bool> OccupiesAsync(Guid userId, Guid unitId) =>
        _db.UnitMemberships
            .AsNoTracking()
            .AnyAsync(membership =>
                membership.UserId == userId
                && membership.UnitId == unitId
                && membership.IsActive
                && membership.EndedAt == null);

    private static ApplicationUser User(string name, string email)
    {
        var user = new ApplicationUser(name, email, null);
        user.NormalizedUserName = email.ToUpperInvariant();
        user.NormalizedEmail = email.ToUpperInvariant();
        return user;
    }
}
