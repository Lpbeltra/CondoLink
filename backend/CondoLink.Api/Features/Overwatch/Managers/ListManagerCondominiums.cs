using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class ListManagerCondominiums
{
    public static IEndpointRouteBuilder MapListManagerCondominiums(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/managers/{managerId:guid}/condominiums",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("List manager condominiums");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid managerId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var managerExists = await (
            from userRole in dbContext.UserRoles
            join role in dbContext.Roles on userRole.RoleId equals role.Id
            where userRole.UserId == managerId && role.Name == "Manager"
            select userRole).AnyAsync(cancellationToken);
        if (!managerExists)
        {
            return Results.NotFound(new { message = "Manager not found." });
        }

        var items = await (
            from membership in dbContext.CondominiumMemberships.AsNoTracking()
            join role in dbContext.CondominiumMembershipRoles.AsNoTracking()
                on membership.Id equals role.CondominiumMembershipId
            join condominium in dbContext.Condominiums.AsNoTracking()
                on membership.CondominiumId equals condominium.Id
            join company in dbContext.ManagementCompanies.AsNoTracking()
                on condominium.ManagementCompanyId equals company.Id into companies
            from company in companies.DefaultIfEmpty()
            where membership.UserId == managerId &&
                membership.IsActive &&
                membership.EndedAt == null &&
                role.Role == CondominiumRole.Manager &&
                role.IsActive &&
                role.RevokedAt == null
            orderby condominium.Name
            select new ManagerCondominiumResponse(
                membership.Id,
                condominium.Id,
                condominium.Name,
                company == null ? null : company.Name,
                condominium.IsActive,
                membership.JoinedAt))
            .ToListAsync(cancellationToken);
        return Results.Ok(items);
    }
}
