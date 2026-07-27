using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Overwatch.Managers;

public static class CreateOverwatchManagementMembership
{
    public static IEndpointRouteBuilder MapCreateOverwatchManagementMembership(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/overwatch/management-memberships",
                HandleAsync)
            .RequireAuthorization("PlatformAdmin");

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Request request,
        ManagerOnboardingService onboardingService,
        CondoLink.Infrastructure.Persistence.AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (request.ManagerId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                error = "ManagerId is required."
            });
        }

        if (request.CondominiumId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                error = "CondominiumId is required."
            });
        }

        var result = await onboardingService.OnboardAsync(
            request.ManagerId,
            request.CondominiumId,
            cancellationToken);

        if (!result.Succeeded)
        {
            if (result.IsConflict)
            {
                return Results.Conflict(new
                {
                    error = result.Error
                });
            }

            return Results.NotFound(new
            {
                error = result.Error
            });
        }

        var response = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join role in dbContext.CondominiumMembershipRoles.AsNoTracking()
                    on membership.Id equals role.CondominiumMembershipId
                join user in dbContext.Users.AsNoTracking()
                    on membership.UserId equals user.Id
                where membership.Id == result.MembershipId
                    && role.Role == CondoLink.Domain.Enums.CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null
                select new CondominiumManagerResponse(
                    membership.Id,
                    user.Id,
                    user.FullName,
                    user.Email!,
                    user.PhoneNumber,
                    user.IsActive,
                    membership.JoinedAt))
            .SingleAsync(cancellationToken);

        return Results.Created(
            $"/overwatch/management-memberships/{response.MembershipId}",
            response);
    }

    public sealed record Request(
        Guid ManagerId,
        Guid CondominiumId);
}
