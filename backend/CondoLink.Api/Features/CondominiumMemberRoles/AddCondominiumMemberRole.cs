using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.CondominiumMemberRoles;

public static class AddCondominiumMemberRole
{
    public static IEndpointRouteBuilder MapAddCondominiumMemberRole(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/condominium-memberships/{membershipId:guid}/roles",
                HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid membershipId,
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

        var membership = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership => membership.Id == membershipId)
            .Select(membership => new
            {
                membership.UserId,
                membership.CondominiumId,
                membership.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (membership is null)
        {
            return Results.NotFound(
                new { error = "Condominium membership not found." });
        }

        var isCondominiumManager = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(managerMembership =>
                managerMembership.UserId == authenticatedUserId
                && managerMembership.CondominiumId == membership.CondominiumId
                && managerMembership.IsActive
                && managerMembership.EndedAt == null)
            .Join(
                dbContext.CondominiumMembershipRoles
                    .AsNoTracking()
                    .Where(role =>
                        role.Role == CondominiumRole.Manager
                        && role.IsActive
                        && role.RevokedAt == null),
                managerMembership => managerMembership.Id,
                role => role.CondominiumMembershipId,
                (_, _) => true)
            .AnyAsync(cancellationToken);

        if (!isCondominiumManager)
        {
            return Results.Json(
                new { error = "Only condominium managers can assign member roles." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!membership.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive membership cannot receive new roles." });
        }

        var condominiumIsActive = await dbContext.Condominiums
            .AsNoTracking()
            .Where(condominium => condominium.Id == membership.CondominiumId)
            .Select(condominium => condominium.IsActive)
            .SingleAsync(cancellationToken);

        if (!condominiumIsActive)
        {
            return Results.Conflict(new
            {
                error = "Inactive condominium cannot receive new member roles."
            });
        }

        var linkedUserIsActive = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(user => user.Id == membership.UserId)
            .Select(user => user.IsActive)
            .SingleAsync(cancellationToken);

        if (!linkedUserIsActive)
        {
            return Results.Conflict(new
            {
                error = "Inactive user cannot receive new condominium roles."
            });
        }

        if (!TryParseRole(request.Role, out var role))
        {
            return Results.BadRequest(
                new { error = "Role must be Manager or Resident." });
        }

        var alreadyExists = await dbContext.CondominiumMembershipRoles
            .AsNoTracking()
            .AnyAsync(
                membershipRole =>
                    membershipRole.CondominiumMembershipId == membershipId
                    && membershipRole.Role == role,
                cancellationToken);

        if (alreadyExists)
        {
            return DuplicateRoleConflict();
        }

        var membershipRole = new CondominiumMembershipRole(membershipId, role);
        dbContext.CondominiumMembershipRoles.Add(membershipRole);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateRoleViolation(exception))
        {
            return DuplicateRoleConflict();
        }

        var response = new Response(
            membershipRole.Id,
            membershipRole.CondominiumMembershipId,
            membershipRole.Role.ToString(),
            membershipRole.IsActive,
            membershipRole.GrantedAt,
            membershipRole.RevokedAt);

        return Results.Created(
            $"/condominium-membership-roles/{membershipRole.Id}",
            response);
    }

    private static bool TryParseRole(string? value, out CondominiumRole role)
    {
        role = default;

        return !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse(value, ignoreCase: true, out role)
            && Enum.IsDefined(role);
    }

    private static bool IsDuplicateRoleViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName:
                CondominiumMembershipRoleConfiguration.UniqueMembershipRoleIndex
        };
    }

    private static IResult DuplicateRoleConflict()
    {
        return Results.Conflict(new
        {
            error = "This role is already associated with the condominium membership."
        });
    }

    public sealed record Request(string? Role);

    public sealed record Response(
        Guid Id,
        Guid CondominiumMembershipId,
        string Role,
        bool IsActive,
        DateTime GrantedAt,
        DateTime? RevokedAt);
}
