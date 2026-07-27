using CondoLink.Api.Features.Overwatch.Managers;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public static class ReplaceOverwatchCondominiumManager
{
    public static IEndpointRouteBuilder MapReplaceOverwatchCondominiumManager(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut(
                "/overwatch/condominiums/{condominiumId:guid}/manager",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Overwatch")
            .WithSummary("Link or transactionally replace a condominium manager");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        Request request,
        ManagerOnboardingService onboardingService,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.ManagerId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "ManagerId é obrigatório." });
        }

        var result = await onboardingService.ReplaceAsync(
            request.ManagerId, condominiumId, cancellationToken);
        if (!result.Succeeded)
        {
            return result.IsConflict
                ? Results.Conflict(new { error = result.Error })
                : Results.NotFound(new { error = result.Error });
        }

        var manager = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join role in dbContext.CondominiumMembershipRoles.AsNoTracking()
                    on membership.Id equals role.CondominiumMembershipId
                join user in dbContext.Users.AsNoTracking()
                    on membership.UserId equals user.Id
                where membership.CondominiumId == condominiumId
                    && membership.UserId == request.ManagerId
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
            .SingleAsync(cancellationToken);

        return Results.Ok(manager);
    }

    public sealed record Request(Guid ManagerId);
}
