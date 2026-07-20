using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class AddCondominiumMember
{
    public static IEndpointRouteBuilder MapAddCondominiumMember(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/condominiums/{condominiumId:guid}/members",
                HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        Request request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CondominiumMembershipService membershipService,
        CancellationToken cancellationToken)
    {
        var authenticatedUserIdValue =
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(
                authenticatedUserIdValue,
                out var authenticatedUserId))
        {
            return Results.Json(
                new
                {
                    error = "Invalid authenticated user."
                },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var authenticatedUser = await dbContext
            .Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == authenticatedUserId)
            .Select(user => new
            {
                user.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (authenticatedUser is null)
        {
            return Results.Json(
                new
                {
                    error = "Authenticated user was not found."
                },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!authenticatedUser.IsActive)
        {
            return Results.Json(
                new
                {
                    error = "User account is inactive."
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var isCondominiumManager =
            await dbContext.CondominiumMemberships
                .AsNoTracking()
                .Where(membership =>
                    membership.UserId == authenticatedUserId &&
                    membership.CondominiumId == condominiumId &&
                    membership.IsActive &&
                    membership.EndedAt == null)
                .Join(
                    dbContext.CondominiumMembershipRoles
                        .AsNoTracking()
                        .Where(role =>
                            role.Role == CondominiumRole.Manager &&
                            role.IsActive &&
                            role.RevokedAt == null),
                    membership => membership.Id,
                    role => role.CondominiumMembershipId,
                    (_, _) => true)
                .AnyAsync(cancellationToken);

        if (!isCondominiumManager)
        {
            return Results.Json(
                new
                {
                    error = "Only condominium managers can add members."
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var result = await membershipService.AddMemberAsync(
            condominiumId,
            request.UserId,
            cancellationToken);

        if (!result.Succeeded)
        {
            return MapFailure(result);
        }

        var membership = result.Membership!;

        return Results.Created(
            $"/condominium-memberships/{membership.Id}",
            new Response(
                membership.Id,
                membership.UserId,
                membership.CondominiumId,
                membership.IsActive,
                membership.JoinedAt,
                membership.EndedAt,
                membership.CreatedAt));
    }

    private static IResult MapFailure(AddMemberResult result)
    {
        var response = new
        {
            error = result.ErrorMessage
        };

        return result.Error switch
        {
            AddMemberError.InvalidUserId =>
                Results.BadRequest(response),

            AddMemberError.CondominiumNotFound or
            AddMemberError.UserNotFound =>
                Results.NotFound(response),

            AddMemberError.InactiveCondominium or
            AddMemberError.InactiveUser or
            AddMemberError.DuplicateMembership =>
                Results.Conflict(response),

            _ => Results.BadRequest(response)
        };
    }

    public sealed record Request(Guid UserId);

    public sealed record Response(
        Guid Id,
        Guid UserId,
        Guid CondominiumId,
        bool IsActive,
        DateTime JoinedAt,
        DateTime? EndedAt,
        DateTime CreatedAt);
}