using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.CondominiumMembers;

public static class ListCondominiumMembers
{
    public static IEndpointRouteBuilder MapListCondominiumMembers(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/condominiums/{condominiumId:guid}/members",
                HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
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

        var condominiumExists = await dbContext.Condominiums
            .AsNoTracking()
            .AnyAsync(
                condominium => condominium.Id == condominiumId,
                cancellationToken);

        if (!condominiumExists)
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
                new { error = "Only condominium managers can view members." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var activeRoles = dbContext.CondominiumMembershipRoles
            .AsNoTracking()
            .Where(role => role.IsActive && role.RevokedAt == null);

        var rows = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join user in dbContext.Set<ApplicationUser>().AsNoTracking()
                    on membership.UserId equals user.Id
                join role in activeRoles
                    on membership.Id equals role.CondominiumMembershipId into roles
                from role in roles.DefaultIfEmpty()
                where membership.CondominiumId == condominiumId
                orderby user.FullName
                select new
                {
                    MembershipId = membership.Id,
                    membership.UserId,
                    user.FullName,
                    user.Email,
                    user.PhoneNumber,
                    MembershipActive = membership.IsActive,
                    membership.JoinedAt,
                    membership.EndedAt,
                    Role = role == null ? (CondominiumRole?)null : role.Role
                })
            .ToListAsync(cancellationToken);

        var response = rows
            .GroupBy(row => new
            {
                row.MembershipId,
                row.UserId,
                row.FullName,
                row.Email,
                row.PhoneNumber,
                row.MembershipActive,
                row.JoinedAt,
                row.EndedAt
            })
            .Select(group => new Response(
                group.Key.MembershipId,
                group.Key.UserId,
                group.Key.FullName,
                group.Key.Email!,
                group.Key.PhoneNumber,
                group.Key.MembershipActive,
                group.Key.JoinedAt,
                group.Key.EndedAt,
                group
                    .Where(row => row.Role.HasValue)
                    .OrderBy(row => row.Role)
                    .Select(row => row.Role!.Value.ToString())
                    .ToArray()))
            .OrderBy(member => member.FullName)
            .ToArray();

        return Results.Ok(response);
    }

    public sealed record Response(
        Guid MembershipId,
        Guid UserId,
        string FullName,
        string Email,
        string? PhoneNumber,
        bool MembershipActive,
        DateTime JoinedAt,
        DateTime? EndedAt,
        IReadOnlyList<string> Roles);
}
