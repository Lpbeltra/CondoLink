using CondoLink.Api.Features.Overwatch.SubManagers;
using CondoLink.Api.Features.Management;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Tests;

/// <summary>
/// SubManager uniqueness (global-per-user) is
/// enforced by an advisory-lock + DB trigger (see migration
/// AddManagementCompanyFoundation, function enforce_single_active_submanager_role), but
/// had zero concurrency coverage anywhere in the suite. These tests exercise the real
/// SubManagerEndpoints.AssignAsync path (made internal for this purpose) against a real
/// Postgres database, wrapped in the same explicit transaction the production callers
/// (CreateAsync/UpdateAsync) already use — the advisory lock only holds for the lifetime
/// of a transaction, so calling AssignAsync without one would not exercise the real guard.
/// </summary>
public sealed class SubManagerPostgresConcurrencyTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("COMVY_TEST_POSTGRES");

    [Fact]
    public async Task Same_user_cannot_become_active_submanager_of_two_condominiums_concurrently()
    {
        if (Connection is null) return;
        var (userId, condoAId, condoBId) = await Seed();
        var outcomes = await Task.WhenAll(AssignSafe(userId, condoAId), AssignSafe(userId, condoBId));
        Assert.Equal(1, outcomes.Count(x => x.Succeeded));

        await using var db = Db();
        var activeCount = await (from m in db.CondominiumMemberships
            join r in db.CondominiumMembershipRoles on m.Id equals r.CondominiumMembershipId
            where m.UserId == userId && m.IsActive && m.EndedAt == null
                && r.Role == CondoLink.Domain.Enums.CondominiumRole.SubManager && r.IsActive && r.RevokedAt == null
            select r.Id).CountAsync();
        Assert.Equal(1, activeCount);
    }

    [Fact]
    public async Task Two_users_can_become_active_submanagers_of_the_same_condominium_concurrently()
    {
        if (Connection is null) return;
        var (userAId, userBId, condoId) = await SeedTwoUsers();
        var outcomes = await Task.WhenAll(AssignSafe(userAId, condoId), AssignSafe(userBId, condoId));
        Assert.Equal(2, outcomes.Count(x => x.Succeeded));

        await using var db = Db();
        var activeCount = await (from m in db.CondominiumMemberships
            join r in db.CondominiumMembershipRoles on m.Id equals r.CondominiumMembershipId
            where m.CondominiumId == condoId && m.IsActive && m.EndedAt == null
                && r.Role == CondoLink.Domain.Enums.CondominiumRole.SubManager && r.IsActive && r.RevokedAt == null
            select r.Id).CountAsync();
        Assert.Equal(2, activeCount);
    }

    [Fact]
    public async Task Permissions_are_independent_per_submanager_membership()
    {
        if (Connection is null) return;
        var (userAId, userBId, condoId) = await SeedTwoUsers();
        Assert.True((await AssignSafe(userAId, condoId)).Succeeded);
        Assert.True((await AssignSafe(userBId, condoId)).Succeeded);
        await using var db = Db();
        var membershipA = await ActiveMembership(userAId, condoId, db);
        var membershipB = await ActiveMembership(userBId, condoId, db);
        await SubManagerAccess.EnsureDefaultsAsync(db, membershipA, userAId, default);
        await SubManagerAccess.EnsureDefaultsAsync(db, membershipB, userBId, default);
        var aAgenda = await db.SubManagerModulePermissions.SingleAsync(x => x.CondominiumMembershipId == membershipA && x.Module == SubManagerModule.Agenda);
        var aCompany = await db.SubManagerModulePermissions.SingleAsync(x => x.CondominiumMembershipId == membershipA && x.Module == SubManagerModule.ManagementCompany);
        var bRequests = await db.SubManagerModulePermissions.SingleAsync(x => x.CondominiumMembershipId == membershipB && x.Module == SubManagerModule.Requests);
        aAgenda.SetAllowed(false, userBId); aCompany.SetAllowed(false, userBId); bRequests.SetAllowed(false, userAId);
        await db.SaveChangesAsync();
        Assert.True(await SubManagerAccess.HasAsync(db, userAId, condoId, SubManagerModule.Requests, default));
        Assert.False(await SubManagerAccess.HasAsync(db, userAId, condoId, SubManagerModule.Agenda, default));
        Assert.False(await SubManagerAccess.HasAsync(db, userAId, condoId, SubManagerModule.ManagementCompany, default));
        Assert.False(await SubManagerAccess.HasAsync(db, userBId, condoId, SubManagerModule.Requests, default));
        Assert.True(await SubManagerAccess.HasAsync(db, userBId, condoId, SubManagerModule.Agenda, default));
        Assert.True(await SubManagerAccess.HasAsync(db, userBId, condoId, SubManagerModule.ManagementCompany, default));
        aAgenda.SetAllowed(true, userAId); bRequests.SetAllowed(true, userBId);
        await db.SaveChangesAsync();
        Assert.True(await SubManagerAccess.HasAsync(db, userAId, condoId, SubManagerModule.Agenda, default));
        Assert.True(await SubManagerAccess.HasAsync(db, userBId, condoId, SubManagerModule.Requests, default));
    }

    private static async Task<Guid> ActiveMembership(Guid userId, Guid condominiumId, AppDbContext db) =>
        await db.CondominiumMemberships.Where(x => x.UserId == userId && x.CondominiumId == condominiumId && x.IsActive && x.EndedAt == null).Select(x => x.Id).SingleAsync();

    private static async Task<(Guid UserId, Guid CondoAId, Guid CondoBId)> Seed()
    {
        await using var db = Db();
        var user = CoreTestSeed.User("Candidato", $"candidato-{Guid.NewGuid():N}@test.local");
        var condoA = new Condominium("Concorrência SubManager A", null, null);
        var condoB = new Condominium("Concorrência SubManager B", null, null);
        db.AddRange(user, condoA, condoB);
        await db.SaveChangesAsync();
        return (user.Id, condoA.Id, condoB.Id);
    }

    private static async Task<(Guid UserAId, Guid UserBId, Guid CondoId)> SeedTwoUsers()
    {
        await using var db = Db();
        var userA = CoreTestSeed.User("Candidato A", $"candidato-a-{Guid.NewGuid():N}@test.local");
        var userB = CoreTestSeed.User("Candidato B", $"candidato-b-{Guid.NewGuid():N}@test.local");
        var condo = new Condominium("Concorrência SubManager Único", null, null);
        db.AddRange(userA, userB, condo);
        await db.SaveChangesAsync();
        return (userA.Id, userB.Id, condo.Id);
    }

    private static async Task<(bool Succeeded, string? Message)> AssignSafe(Guid userId, Guid condominiumId)
    {
        await using var db = Db();
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var message = await SubManagerEndpoints.AssignAsync(user, condominiumId, db, default);
            await transaction.CommitAsync();
            return (message is null, message);
        }
        catch (DbUpdateException exception)
        {
            return (false, exception.Message);
        }
    }

    private static AppDbContext Db()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options;
        return new AppDbContext(options);
    }
}
