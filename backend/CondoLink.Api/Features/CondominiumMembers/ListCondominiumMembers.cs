using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure;
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

        if (!isCondominiumManager
            && !principal.IsInRole(
                DependencyInjection.PlatformAdminRole))
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
                    user.Cpf,
                    user.Cnpj,
                    user.Address,
                    user.City,
                    user.State,
                    UserActive = user.IsActive,
                    user.MustChangePassword,
                    user.LastLoginAt,
                    MembershipActive = membership.IsActive,
                    membership.JoinedAt,
                    membership.EndedAt,
                    Role = role == null ? (CondominiumRole?)null : role.Role
                })
            .ToListAsync(cancellationToken);

        var unitLinks = await (
                from link in dbContext.UnitMemberships.AsNoTracking()
                join unit in dbContext.Units.AsNoTracking()
                    on link.UnitId equals unit.Id
                join block in dbContext.CondominiumBlocks.AsNoTracking()
                    on unit.BlockId equals block.Id into blocks
                from block in blocks.DefaultIfEmpty()
                where unit.CondominiumId == condominiumId
                    && link.IsActive
                    && link.EndedAt == null
                select new
                {
                    link.UserId,
                    Link = new UnitLinkResponse(
                        link.Id,
                        unit.Id,
                        unit.Identifier,
                        block == null ? null : block.Identifier,
                        link.RelationshipType.ToString(),
                        link.IsResident,
                        link.IsPrimaryResidence)
                })
            .ToListAsync(cancellationToken);
        var linksByUser = unitLinks
            .GroupBy(item => item.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<UnitLinkResponse>)group
                    .Select(item => item.Link)
                    .OrderBy(item => item.Block)
                    .ThenBy(item => item.UnitIdentifier)
                    .ToArray());

        var response = rows
            .GroupBy(row => new
            {
                row.MembershipId,
                row.UserId,
                row.FullName,
                row.Email,
                row.PhoneNumber,
                row.Cpf,
                row.Cnpj,
                row.Address,
                row.City,
                row.State,
                row.UserActive,
                row.MustChangePassword,
                row.LastLoginAt,
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
                group.Key.Cpf,
                group.Key.Cnpj,
                group.Key.Address,
                group.Key.City,
                group.Key.State,
                group.Key.UserActive,
                group.Key.MustChangePassword,
                group.Key.LastLoginAt,
                group.Key.MembershipActive,
                group.Key.JoinedAt,
                group.Key.EndedAt,
                group
                    .Where(row => row.Role.HasValue)
                    .OrderBy(row => row.Role)
                    .Select(row => row.Role!.Value.ToString())
                    .ToArray(),
                linksByUser.GetValueOrDefault(
                    group.Key.UserId,
                    [])))
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
        string? Cpf,
        string? Cnpj,
        string? Address,
        string? City,
        string? State,
        bool UserActive,
        bool MustChangePassword,
        DateTime? LastLoginAt,
        bool MembershipActive,
        DateTime JoinedAt,
        DateTime? EndedAt,
        IReadOnlyList<string> Roles,
        IReadOnlyList<UnitLinkResponse> UnitLinks);

    public sealed record UnitLinkResponse(
        Guid UnitMembershipId,
        Guid UnitId,
        string UnitIdentifier,
        string? Block,
        string RelationshipType,
        bool IsResident,
        bool IsPrimaryResidence);
}
