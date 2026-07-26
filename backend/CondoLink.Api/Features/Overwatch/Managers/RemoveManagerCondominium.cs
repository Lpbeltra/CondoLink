using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class RemoveManagerCondominium
{
    public static IEndpointRouteBuilder MapRemoveManagerCondominium(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete(
                "/overwatch/managers/{managerId:guid}/condominiums/{condominiumId:guid}",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Remove manager from condominium");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managerId,
        Guid condominiumId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var manager = await dbContext.Users
            .SingleOrDefaultAsync(user => user.Id == managerId, cancellationToken);
        if (manager is null)
        {
            return Results.NotFound(new { message = "Manager not found." });
        }

        if (!await dbContext.Condominiums.AnyAsync(
                item => item.Id == condominiumId, cancellationToken))
        {
            return Results.NotFound(new { message = "Condominium not found." });
        }

        var role = await (
            from membership in dbContext.CondominiumMemberships
            join membershipRole in dbContext.CondominiumMembershipRoles
                on membership.Id equals membershipRole.CondominiumMembershipId
            where membership.UserId == managerId &&
                membership.CondominiumId == condominiumId &&
                membershipRole.Role == CondominiumRole.Manager &&
                membershipRole.IsActive &&
                membershipRole.RevokedAt == null
            select membershipRole)
            .SingleOrDefaultAsync(cancellationToken);
        if (role is null)
        {
            return Results.NotFound(new { message = "Manager association not found." });
        }

        role.Deactivate();
        if (manager.ActiveManagementCondominiumId == condominiumId)
        {
            manager.ClearActiveManagementCondominium();
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }
}
