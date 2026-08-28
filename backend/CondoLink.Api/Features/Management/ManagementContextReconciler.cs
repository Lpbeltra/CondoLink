using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Management;

public static class ManagementContextReconciler
{
    public static async Task<ManagementContextState> ReconcileAsync(
        ApplicationUser user,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominiums = user.IsActive
            ? await GetAvailableCondominiumsAsync(
                user.Id, dbContext, cancellationToken)
            : [];

        Guid? activeCondominiumId = null;
        if (condominiums.Count == 1)
        {
            activeCondominiumId = condominiums[0].Id;
        }
        else if (condominiums.Count > 1
            && user.ActiveManagementCondominiumId is Guid storedId
            && condominiums.Any(item => item.Id == storedId))
        {
            activeCondominiumId = storedId;
        }

        ApplyActiveCondominium(user, activeCondominiumId);
        return CreateState(activeCondominiumId, condominiums);
    }

    public static async Task<ManagementContextState> SelectAsync(
        ApplicationUser user,
        Guid? condominiumId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominiums = await GetAvailableCondominiumsAsync(
            user.Id, dbContext, cancellationToken);

        Guid? selectedId;
        if (condominiumId is null || condominiumId == Guid.Empty)
        {
            selectedId = condominiums.Count == 1
                ? condominiums[0].Id
                : null;
        }
        else
        {
            selectedId = condominiumId;
        }

        ApplyActiveCondominium(user, selectedId);
        return CreateState(selectedId, condominiums);
    }

    public static async Task<IReadOnlyList<ManagementCondominiumState>>
        GetAvailableCondominiumsAsync(
            Guid userId,
            AppDbContext dbContext,
            CancellationToken cancellationToken)
    {
        var rows = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join role in dbContext.CondominiumMembershipRoles.AsNoTracking()
                    on membership.Id equals role.CondominiumMembershipId
                join condominium in dbContext.Condominiums.AsNoTracking()
                    on membership.CondominiumId equals condominium.Id
                where membership.UserId == userId
                    && membership.IsActive
                    && membership.EndedAt == null
                    && role.IsActive
                    && role.RevokedAt == null
                    && (role.Role == CondominiumRole.Manager
                        || role.Role == CondominiumRole.SubManager)
                    && condominium.IsActive
                select new
                {
                    condominium.Id,
                    condominium.Name,
                    condominium.IsActive
                })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(item => item.Name)
            .Select(item => new ManagementCondominiumState(
                item.Id,
                item.Name,
                item.IsActive))
            .ToArray();
    }

    private static void ApplyActiveCondominium(
        ApplicationUser user,
        Guid? activeCondominiumId)
    {
        if (user.ActiveManagementCondominiumId == activeCondominiumId)
        {
            return;
        }

        if (activeCondominiumId.HasValue)
        {
            user.SetActiveManagementCondominium(activeCondominiumId.Value);
        }
        else
        {
            user.ClearActiveManagementCondominium();
        }
    }

    private static ManagementContextState CreateState(
        Guid? activeCondominiumId,
        IReadOnlyList<ManagementCondominiumState> condominiums)
        => new(
            activeCondominiumId,
            condominiums.Count > 1 && activeCondominiumId is null,
            condominiums.Count,
            activeCondominiumId is Guid id
                ? condominiums.Single(item => item.Id == id)
                : null,
            condominiums);
}

public sealed record ManagementCondominiumState(
    Guid Id,
    string Name,
    bool IsActive);

public sealed record ManagementContextState(
    Guid? ActiveManagementCondominiumId,
    bool UsesConsolidatedManagementScope,
    int CondominiumCount,
    ManagementCondominiumState? ActiveCondominium,
    IReadOnlyList<ManagementCondominiumState> AvailableCondominiums);
