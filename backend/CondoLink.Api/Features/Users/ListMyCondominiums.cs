using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Users;

public static class ListMyCondominiums
{
    public static IEndpointRouteBuilder MapListMyCondominiums(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/users/me/condominiums", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
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

        var activeRoles = dbContext.CondominiumMembershipRoles
            .AsNoTracking()
            .Where(role => role.IsActive && role.RevokedAt == null);

        var rows = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join condominium in dbContext.Condominiums.AsNoTracking()
                    on membership.CondominiumId equals condominium.Id
                join role in activeRoles
                    on membership.Id equals role.CondominiumMembershipId into roles
                from role in roles.DefaultIfEmpty()
                where membership.UserId == authenticatedUserId
                    && membership.IsActive
                    && membership.EndedAt == null
                    && condominium.IsActive
                orderby condominium.Name
                select new
                {
                    MembershipId = membership.Id,
                    CondominiumId = condominium.Id,
                    CondominiumName = condominium.Name,
                    CondominiumActive = condominium.IsActive,
                    membership.JoinedAt,
                    MembershipActive = membership.IsActive,
                    Role = role == null ? (CondominiumRole?)null : role.Role
                })
            .ToListAsync(cancellationToken);

        var response = rows
            .GroupBy(row => new
            {
                row.MembershipId,
                row.CondominiumId,
                row.CondominiumName,
                row.CondominiumActive,
                row.JoinedAt,
                row.MembershipActive
            })
            .Select(group => new Response(
                group.Key.MembershipId,
                new CondominiumResponse(
                    group.Key.CondominiumId,
                    group.Key.CondominiumName,
                    group.Key.CondominiumActive),
                group
                    .Where(row => row.Role.HasValue)
                    .OrderBy(row => row.Role)
                    .Select(row => row.Role!.Value.ToString())
                    .ToArray(),
                group.Key.JoinedAt,
                group.Key.MembershipActive))
            .OrderBy(item => item.Condominium.Name)
            .ToArray();

        return Results.Ok(response);
    }

    public sealed record CondominiumResponse(
        Guid Id,
        string Name,
        bool IsActive);

    public sealed record Response(
        Guid MembershipId,
        CondominiumResponse Condominium,
        IReadOnlyList<string> Roles,
        DateTime JoinedAt,
        bool MembershipActive);
}
