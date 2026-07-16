using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class AddCondominiumMember
{
    public static IEndpointRouteBuilder MapAddCondominiumMember(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums/{condominiumId:guid}/members", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        Request request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var authenticatedUserIdValue =
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(authenticatedUserIdValue, out var authenticatedUserId))
        {
            return Results.Json(
                new { error = "Invalid authenticated user." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        var authenticatedUser = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == authenticatedUserId)
            .Select(user => new { user.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (authenticatedUser is null)
        {
            return Results.Json(
                new { error = "Authenticated user was not found." },
                statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!authenticatedUser.IsActive)
        {
            return Results.Json(
                new { error = "User account is inactive." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (request.UserId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "UserId is required." });
        }

        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(condominium => condominium.Id == condominiumId)
            .Select(condominium => new { condominium.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new { error = "Condominium not found." });
        }

        var isCondominiumManager = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == authenticatedUserId
                && membership.CondominiumId == condominiumId
                && membership.IsActive
                && membership.EndedAt == null)
            .Join(
                dbContext.CondominiumMembershipRoles
                    .AsNoTracking()
                    .Where(role =>
                        role.Role == CondominiumRole.Manager
                        && role.IsActive
                        && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (_, _) => true)
            .AnyAsync(cancellationToken);

        if (!isCondominiumManager)
        {
            return Results.Json(
                new { error = "Only condominium managers can add members." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!condominium.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive condominium cannot receive new members." });
        }

        var user = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == request.UserId)
            .Select(user => new { user.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Results.NotFound(new { error = "User not found." });
        }

        if (!user.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive user cannot be added to a condominium." });
        }

        var alreadyExists = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .AnyAsync(
                membership => membership.UserId == request.UserId
                    && membership.CondominiumId == condominiumId,
                cancellationToken);

        if (alreadyExists)
        {
            return DuplicateMembershipConflict();
        }

        var membership = new CondominiumMembership(request.UserId, condominiumId);
        dbContext.CondominiumMemberships.Add(membership);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateMembershipViolation(exception))
        {
            return DuplicateMembershipConflict();
        }

        var response = new Response(
            membership.Id,
            membership.UserId,
            membership.CondominiumId,
            membership.IsActive,
            membership.JoinedAt,
            membership.EndedAt,
            membership.CreatedAt);

        return Results.Created($"/condominium-memberships/{membership.Id}", response);
    }

    private static bool IsDuplicateMembershipViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName:
                CondominiumMembershipConfiguration.UniqueUserCondominiumIndex
        };
    }

    private static IResult DuplicateMembershipConflict()
    {
        return Results.Conflict(new
        {
            error = "User is already associated with this condominium."
        });
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
