using System.Collections.Concurrent;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Api.Features.Management;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public sealed class ManagerOnboardingService(AppDbContext dbContext)
{
    private const string DuplicateMessage =
        "Este síndico já está vinculado ao condomínio.";
    private const string OccupiedMessage =
        "Este condomínio já possui um síndico vinculado.";
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> LocalLocks = new();

    public async Task<ManagerOnboardingResult> OnboardAsync(
        Guid managerId,
        Guid condominiumId,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(managerId, condominiumId, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var gate = LocalLocks.GetOrAdd(condominiumId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await AcquireDatabaseLockAsync(condominiumId, cancellationToken);
            var lockedValidation =
                await ValidateAsync(managerId, condominiumId, cancellationToken);
            if (lockedValidation is not null)
            {
                return lockedValidation;
            }

            var membership = await dbContext.CondominiumMemberships
                .SingleOrDefaultAsync(
                    current => current.UserId == managerId
                        && current.CondominiumId == condominiumId,
                    cancellationToken);

            var managerRole = membership is null
                ? null
                : await dbContext.CondominiumMembershipRoles.SingleOrDefaultAsync(
                    current => current.CondominiumMembershipId == membership.Id
                        && current.Role == CondominiumRole.Manager,
                    cancellationToken);

            if (membership is { IsActive: true, EndedAt: null }
                && managerRole is { IsActive: true, RevokedAt: null })
            {
                return ManagerOnboardingResult.Conflict(DuplicateMessage);
            }

            if (await HasAnotherActiveManagerAsync(
                    condominiumId, managerId, cancellationToken))
            {
                return ManagerOnboardingResult.Conflict(OccupiedMessage);
            }

            if (membership is null)
            {
                membership = new CondominiumMembership(managerId, condominiumId);
                dbContext.CondominiumMemberships.Add(membership);
            }
            else
            {
                membership.Activate();
            }

            if (managerRole is null)
            {
                dbContext.CondominiumMembershipRoles.Add(
                    new CondominiumMembershipRole(
                        membership.Id,
                        CondominiumRole.Manager));
            }
            else
            {
                managerRole.Activate();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await ReconcileUserAsync(managerId, cancellationToken);
            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return ManagerOnboardingResult.Success(membership.Id);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ManagerAssignmentResult> ReplaceAsync(
        Guid managerId,
        Guid condominiumId,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateAsync(managerId, condominiumId, cancellationToken);
        if (validation is not null)
        {
            return ManagerAssignmentResult.From(validation);
        }

        var gate = LocalLocks.GetOrAdd(condominiumId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await AcquireDatabaseLockAsync(condominiumId, cancellationToken);
            var lockedValidation =
                await ValidateAsync(managerId, condominiumId, cancellationToken);
            if (lockedValidation is not null)
            {
                return ManagerAssignmentResult.From(lockedValidation);
            }

            var activeLinks = await (
                    from membership in dbContext.CondominiumMemberships
                    join role in dbContext.CondominiumMembershipRoles
                        on membership.Id equals role.CondominiumMembershipId
                    join user in dbContext.Users
                        on membership.UserId equals user.Id
                    where membership.CondominiumId == condominiumId
                        && membership.IsActive
                        && membership.EndedAt == null
                        && role.Role == CondominiumRole.Manager
                        && role.IsActive
                        && role.RevokedAt == null
                        && user.IsActive
                    select new { Membership = membership, Role = role, User = user })
                .ToListAsync(cancellationToken);

            var current = activeLinks.SingleOrDefault();
            if (current?.User.Id == managerId)
            {
                await ManagementContextReconciler.ReconcileAsync(
                    current.User, dbContext, cancellationToken);
                if (dbContext.ChangeTracker.HasChanges())
                {
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
                return ManagerAssignmentResult.Success(current.Membership.Id, false);
            }

            var nextMembership = await dbContext.CondominiumMemberships
                .SingleOrDefaultAsync(
                    item => item.UserId == managerId
                        && item.CondominiumId == condominiumId,
                    cancellationToken);
            if (nextMembership is null)
            {
                nextMembership = new CondominiumMembership(managerId, condominiumId);
                dbContext.CondominiumMemberships.Add(nextMembership);
            }
            else
            {
                nextMembership.Activate();
            }

            var nextRole = await dbContext.CondominiumMembershipRoles
                .SingleOrDefaultAsync(
                    item => item.CondominiumMembershipId == nextMembership.Id
                        && item.Role == CondominiumRole.Manager,
                    cancellationToken);
            if (nextRole is null)
            {
                dbContext.CondominiumMembershipRoles.Add(
                    new CondominiumMembershipRole(
                        nextMembership.Id,
                        CondominiumRole.Manager));
            }
            else
            {
                nextRole.Activate();
            }

            if (current is not null)
            {
                current.Role.Deactivate();
                if (current.User.ActiveManagementCondominiumId == condominiumId)
                {
                    current.User.ClearActiveManagementCondominium();
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            if (current is not null)
            {
                await ReconcileUserAsync(current.User.Id, cancellationToken);
            }
            await ReconcileUserAsync(managerId, cancellationToken);
            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return ManagerAssignmentResult.Success(nextMembership.Id, current is not null);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ManagerAssignmentResult> RemoveAsync(
        Guid managerId,
        Guid condominiumId,
        CancellationToken cancellationToken)
    {
        var gate = LocalLocks.GetOrAdd(condominiumId, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await AcquireDatabaseLockAsync(condominiumId, cancellationToken);

            var manager = await dbContext.Users
                .SingleOrDefaultAsync(user => user.Id == managerId, cancellationToken);
            if (manager is null)
            {
                return ManagerAssignmentResult.NotFound("Síndico não encontrado.");
            }

            if (!await dbContext.Condominiums.AnyAsync(
                    item => item.Id == condominiumId, cancellationToken))
            {
                return ManagerAssignmentResult.NotFound("Condomínio não encontrado.");
            }

            var role = await (
                    from membership in dbContext.CondominiumMemberships
                    join membershipRole in dbContext.CondominiumMembershipRoles
                        on membership.Id equals membershipRole.CondominiumMembershipId
                    where membership.UserId == managerId
                        && membership.CondominiumId == condominiumId
                        && membershipRole.Role == CondominiumRole.Manager
                        && membershipRole.IsActive
                        && membershipRole.RevokedAt == null
                    select membershipRole)
                .SingleOrDefaultAsync(cancellationToken);
            if (role is null)
            {
                return ManagerAssignmentResult.NotFound(
                    "O vínculo informado não está mais disponível.");
            }

            role.Deactivate();
            if (manager.ActiveManagementCondominiumId == condominiumId)
            {
                manager.ClearActiveManagementCondominium();
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await ManagementContextReconciler.ReconcileAsync(
                manager, dbContext, cancellationToken);
            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return ManagerAssignmentResult.Success(null, false);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<ManagerAssignmentResult> SetStatusAsync(
        Guid managerId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var manager = await dbContext.Users.Where(user => user.Id == managerId && (
                dbContext.UserRoles.Any(userRole => userRole.UserId == user.Id
                    && dbContext.Roles.Any(role => role.Id == userRole.RoleId
                        && role.Name == "Manager"))
                || dbContext.CondominiumMemberships.Any(membership =>
                    membership.UserId == user.Id && membership.IsActive
                    && membership.EndedAt == null
                    && dbContext.CondominiumMembershipRoles.Any(role =>
                        role.CondominiumMembershipId == membership.Id
                        && role.Role == CondominiumRole.Manager
                        && role.IsActive && role.RevokedAt == null))))
            .SingleOrDefaultAsync(cancellationToken);
        if (manager is null)
        {
            return ManagerAssignmentResult.NotFound("Síndico não encontrado.");
        }

        if (!isActive)
        {
            manager.SetActiveStatus(false);
            manager.ClearActiveManagementCondominium();
            await dbContext.SaveChangesAsync(cancellationToken);
            return ManagerAssignmentResult.Success(null, false);
        }

        var condominiumIds = await (
                from membership in dbContext.CondominiumMemberships
                join role in dbContext.CondominiumMembershipRoles
                    on membership.Id equals role.CondominiumMembershipId
                where membership.UserId == managerId
                    && membership.IsActive
                    && membership.EndedAt == null
                    && role.Role == CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null
                orderby membership.CondominiumId
                select membership.CondominiumId)
            .ToListAsync(cancellationToken);

        var gates = condominiumIds
            .Distinct()
            .Order()
            .Select(id => LocalLocks.GetOrAdd(id, static _ => new SemaphoreSlim(1, 1)))
            .ToList();
        var acquiredGateCount = 0;
        try
        {
            foreach (var gate in gates)
            {
                await gate.WaitAsync(cancellationToken);
                acquiredGateCount++;
            }

            await using var transaction =
                await dbContext.Database.BeginTransactionAsync(cancellationToken);
            foreach (var condominiumId in condominiumIds.Distinct().Order())
            {
                await AcquireDatabaseLockAsync(condominiumId, cancellationToken);
                if (await HasAnotherActiveManagerAsync(
                        condominiumId, managerId, cancellationToken))
                {
                    return ManagerAssignmentResult.Conflict(
                        "O síndico não pode ser ativado porque um de seus condomínios já possui outro síndico vinculado.");
                }
            }

            manager.SetActiveStatus(true);
            await dbContext.SaveChangesAsync(cancellationToken);
            await ManagementContextReconciler.ReconcileAsync(
                manager, dbContext, cancellationToken);
            if (dbContext.ChangeTracker.HasChanges())
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return ManagerAssignmentResult.Success(null, false);
        }
        finally
        {
            for (var index = acquiredGateCount - 1; index >= 0; index--)
            {
                gates[index].Release();
            }
        }
    }

    private async Task<ManagerOnboardingResult?> ValidateAsync(
        Guid managerId,
        Guid condominiumId,
        CancellationToken cancellationToken)
    {
        var manager = await (
                from user in dbContext.Users
                join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
                join role in dbContext.Roles on userRole.RoleId equals role.Id
                where user.Id == managerId &&
                    (role.Name == "Manager" || role.Name == "PlatformAdmin")
                select new { user.IsActive })
            .Distinct()
            .SingleOrDefaultAsync(cancellationToken);
        if (manager is null)
        {
            return ManagerOnboardingResult.NotFound(
                "O usuário selecionado não pode ser vinculado como síndico.");
        }

        if (!manager.IsActive)
        {
            return ManagerOnboardingResult.Conflict(
                "O síndico selecionado está inativo.");
        }

        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(current => current.Id == condominiumId)
            .Select(current => new { current.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (condominium is null)
        {
            return ManagerOnboardingResult.NotFound("Condomínio não encontrado.");
        }

        return !condominium.IsActive
            ? ManagerOnboardingResult.Conflict(
                "Condomínio inativo não pode receber um síndico.")
            : null;
    }

    private Task<bool> HasAnotherActiveManagerAsync(
        Guid condominiumId,
        Guid managerId,
        CancellationToken cancellationToken)
        => (
            from membership in dbContext.CondominiumMemberships.AsNoTracking()
            join role in dbContext.CondominiumMembershipRoles.AsNoTracking()
                on membership.Id equals role.CondominiumMembershipId
            join user in dbContext.Users.AsNoTracking()
                on membership.UserId equals user.Id
            where membership.CondominiumId == condominiumId
                && membership.UserId != managerId
                && membership.IsActive
                && membership.EndedAt == null
                && role.Role == CondominiumRole.Manager
                && role.IsActive
                && role.RevokedAt == null
                && user.IsActive
            select membership.Id)
        .AnyAsync(cancellationToken);

    private async Task AcquireDatabaseLockAsync(
        Guid condominiumId,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsNpgsql())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({condominiumId.ToString()}, 0));",
                cancellationToken);
        }
    }

    private async Task ReconcileUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleAsync(
            item => item.Id == userId,
            cancellationToken);
        await ManagementContextReconciler.ReconcileAsync(
            user, dbContext, cancellationToken);
    }
}

public sealed record ManagerOnboardingResult(
    bool Succeeded,
    bool IsConflict,
    string? Error,
    Guid? MembershipId)
{
    public static ManagerOnboardingResult Success(Guid membershipId)
        => new(true, false, null, membershipId);

    public static ManagerOnboardingResult NotFound(string error)
        => new(false, false, error, null);

    public static ManagerOnboardingResult Conflict(string error)
        => new(false, true, error, null);
}

public sealed record ManagerAssignmentResult(
    bool Succeeded,
    bool IsConflict,
    string? Error,
    Guid? MembershipId,
    bool Replaced)
{
    public static ManagerAssignmentResult Success(Guid? membershipId, bool replaced)
        => new(true, false, null, membershipId, replaced);

    public static ManagerAssignmentResult NotFound(string error)
        => new(false, false, error, null, false);

    public static ManagerAssignmentResult Conflict(string error)
        => new(false, true, error, null, false);

    public static ManagerAssignmentResult From(ManagerOnboardingResult result)
        => new(
            result.Succeeded,
            result.IsConflict,
            result.Error,
            result.MembershipId,
            false);
}
