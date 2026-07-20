using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public sealed class ManagerOnboardingService(
    AppDbContext dbContext)
{
    public async Task<ManagerOnboardingResult> OnboardAsync(
        Guid managerId,
        Guid condominiumId,
        CancellationToken cancellationToken)
    {
        var manager = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == managerId)
            .Select(user => new
            {
                user.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (manager is null)
        {
            return ManagerOnboardingResult.NotFound(
                "Manager not found.");
        }

        if (!manager.IsActive)
        {
            return ManagerOnboardingResult.Conflict(
                "Inactive manager cannot be associated.");
        }

        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(current => current.Id == condominiumId)
            .Select(current => new
            {
                current.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (condominium is null)
        {
            return ManagerOnboardingResult.NotFound(
                "Condominium not found.");
        }

        if (!condominium.IsActive)
        {
            return ManagerOnboardingResult.Conflict(
                "Inactive condominium cannot receive managers.");
        }

        var membership = await dbContext.CondominiumMemberships
            .FirstOrDefaultAsync(
                current =>
                    current.UserId == managerId &&
                    current.CondominiumId == condominiumId,
                cancellationToken);

        if (membership is null)
        {
            membership = new CondominiumMembership(
                managerId,
                condominiumId);

            dbContext.CondominiumMemberships.Add(membership);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var managerRoleExists =
            await dbContext.CondominiumMembershipRoles
                .AnyAsync(
                    current =>
                        current.CondominiumMembershipId == membership.Id &&
                        current.Role == CondominiumRole.Manager,
                    cancellationToken);

        if (!managerRoleExists)
        {
            dbContext.CondominiumMembershipRoles.Add(
                new CondominiumMembershipRole(
                    membership.Id,
                    CondominiumRole.Manager));

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return ManagerOnboardingResult.Success(
            membership.Id);
    }
}

public sealed record ManagerOnboardingResult(
    bool Succeeded,
    bool IsConflict,
    string? Error,
    Guid? MembershipId)
{
    public static ManagerOnboardingResult Success(
        Guid membershipId)
        => new(true, false, null, membershipId);

    public static ManagerOnboardingResult NotFound(
        string error)
        => new(false, false, error, null);

    public static ManagerOnboardingResult Conflict(
        string error)
        => new(false, true, error, null);
}