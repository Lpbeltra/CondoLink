using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.UnitMemberships;

public static class ListUnitMemberships
{
    public static IEndpointRouteBuilder MapListUnitMemberships(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/units/{unitId:guid}/memberships", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid unitId,
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

        var unit = await dbContext.Units
            .AsNoTracking()
            .Where(unit => unit.Id == unitId)
            .Select(unit => new { unit.CondominiumId })
            .SingleOrDefaultAsync(cancellationToken);

        if (unit is null)
        {
            return Results.NotFound(new { error = "Unit not found." });
        }

        var condominiumExists = await dbContext.Condominiums
            .AsNoTracking()
            .AnyAsync(
                condominium => condominium.Id == unit.CondominiumId,
                cancellationToken);

        if (!condominiumExists)
        {
            return Results.NotFound(new { error = "Condominium not found." });
        }

        var isCondominiumManager = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == authenticatedUserId
                && membership.CondominiumId == unit.CondominiumId
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
                new { error = "Only condominium managers can view unit memberships." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var memberships = await dbContext.UnitMemberships
            .AsNoTracking()
            .Where(membership => membership.UnitId == unitId)
            .Join(
                dbContext.Set<ApplicationUser>().AsNoTracking(),
                membership => membership.UserId,
                user => user.Id,
                (membership, user) => new
                {
                    UnitMembershipId = membership.Id,
                    membership.UserId,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    membership.RelationshipType,
                    membership.IsResident,
                    membership.IsPrimaryResidence,
                    MembershipActive = membership.IsActive,
                    membership.StartedAt,
                    membership.EndedAt,
                    membership.CreatedAt
                })
            .OrderByDescending(item => item.MembershipActive)
            .ThenBy(item => item.FullName)
            .ThenBy(item => item.RelationshipType)
            .ToListAsync(cancellationToken);

        var response = memberships
            .Select(membership => new Response(
                membership.UnitMembershipId,
                membership.UserId,
                membership.FullName,
                membership.Email!,
                membership.PhoneNumber,
                membership.RelationshipType.ToString(),
                membership.IsResident,
                membership.IsPrimaryResidence,
                membership.MembershipActive,
                membership.StartedAt,
                membership.EndedAt,
                membership.CreatedAt))
            .ToArray();

        return Results.Ok(response);
    }

    public sealed record Response(
        Guid UnitMembershipId,
        Guid UserId,
        string FullName,
        string Email,
        string? PhoneNumber,
        string RelationshipType,
        bool IsResident,
        bool IsPrimaryResidence,
        bool MembershipActive,
        DateTime StartedAt,
        DateTime? EndedAt,
        DateTime CreatedAt);
}
