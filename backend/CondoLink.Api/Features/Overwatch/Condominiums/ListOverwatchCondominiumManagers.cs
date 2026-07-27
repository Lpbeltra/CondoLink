using CondoLink.Domain.Enums;
using CondoLink.Api.Features.Overwatch.Managers;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class ListOverwatchCondominiumManagers
{
    public static IEndpointRouteBuilder MapListOverwatchCondominiumManagers(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/condominiums/{id:guid}/managers",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("List condominium managers");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var condominiumExists = await dbContext.Condominiums
            .AnyAsync(condominium => condominium.Id == id, cancellationToken);

        if (!condominiumExists)
        {
            return Results.NotFound(new
            {
                message = "Condominium not found."
            });
        }

        var managers = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join role in dbContext.CondominiumMembershipRoles.AsNoTracking()
                    on membership.Id equals role.CondominiumMembershipId
                join user in dbContext.Users.AsNoTracking()
                    on membership.UserId equals user.Id
                where membership.CondominiumId == id
                    && membership.IsActive
                    && membership.EndedAt == null
                    && role.Role == CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null
                    && user.IsActive
                orderby user.FullName
                select new CondominiumManagerResponse(
                    membership.Id,
                    user.Id,
                    user.FullName,
                    user.Email!,
                    user.PhoneNumber,
                    user.IsActive,
                    membership.JoinedAt))
            .Take(1)
            .ToListAsync(cancellationToken);

        return Results.Ok(managers);
    }
}
