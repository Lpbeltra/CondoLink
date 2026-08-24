using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class ListOverwatchManagers
{
    public static IEndpointRouteBuilder MapListOverwatchManagers(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/managers",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("List managers");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        string? search,
        bool? eligibleForAssignment,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Users
            .AsNoTracking()
            .Where(user => (eligibleForAssignment == true
                && dbContext.UserRoles.Any(userRole =>
                userRole.UserId == user.Id &&
                dbContext.Roles.Any(role =>
                    role.Id == userRole.RoleId &&
                    (role.Name == "Manager" || role.Name == "PlatformAdmin"))))
                || dbContext.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id && dbContext.Roles.Any(role =>
                        role.Id == userRole.RoleId && role.Name == "Manager"))
                || dbContext.CondominiumMemberships.Any(membership =>
                    membership.UserId == user.Id && membership.IsActive
                    && membership.EndedAt == null
                    && dbContext.CondominiumMembershipRoles.Any(role =>
                        role.CondominiumMembershipId == membership.Id
                        && role.Role == Domain.Enums.CondominiumRole.Manager
                        && role.IsActive && role.RevokedAt == null)));

        if (eligibleForAssignment is true)
        {
            query = query.Where(user => user.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(user =>
                EF.Functions.ILike(user.FullName, $"%{term}%") ||
                EF.Functions.ILike(user.Email!, $"%{term}%"));
        }

        var managers = await query
            .OrderBy(user => user.FullName)
            .Select(user => new OverwatchManagerResponse(
                user.Id,
                user.FullName,
                user.Email!,
                user.PhoneNumber,
                user.Cpf,
                user.Cnpj,
                user.Address,
                user.City,
                user.State,
                user.IsActive,
                dbContext.CondominiumMemberships.Count(membership =>
                    membership.UserId == user.Id &&
                    membership.IsActive &&
                    membership.EndedAt == null &&
                    dbContext.CondominiumMembershipRoles.Any(role =>
                        role.CondominiumMembershipId == membership.Id &&
                        role.Role == Domain.Enums.CondominiumRole.Manager &&
                        role.IsActive &&
                        role.RevokedAt == null)),
                user.CreatedAt,
                user.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Results.Ok(managers);
    }

}
