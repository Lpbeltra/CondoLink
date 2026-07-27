using CondoLink.Api.Features.Overwatch.Managers;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class GetOverwatchCondominiumManager
{
    public static IEndpointRouteBuilder MapGetOverwatchCondominiumManager(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/overwatch/condominiums/{id:guid}/manager",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Get the active condominium manager");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Condominiums
                .AnyAsync(item => item.Id == id, cancellationToken))
        {
            return Results.NotFound(new { error = "Condomínio não encontrado." });
        }

        var manager = await (
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
                select new CondominiumManagerResponse(
                    membership.Id,
                    user.Id,
                    user.FullName,
                    user.Email!,
                    user.PhoneNumber,
                    user.IsActive,
                    membership.JoinedAt))
            .SingleOrDefaultAsync(cancellationToken);

        return Results.Ok(manager);
    }
}
