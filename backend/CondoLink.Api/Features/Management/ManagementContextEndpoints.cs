using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Management;

public static class ManagementContextEndpoints
{
    public static IEndpointRouteBuilder MapManagementContext(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/management/context", HandleGetAsync)
            .RequireAuthorization();

        endpoints.MapPut("/management/context", HandlePutAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleGetAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(principal, out var authenticatedUserId))
        {
            return InvalidAuthenticatedUser();
        }

        if (!await IsAuthenticatedUserActive(authenticatedUserId, dbContext, cancellationToken))
        {
            return AuthenticationFailed();
        }

        var availableCondominiums = await GetManagementCondominiumsAsync(
            authenticatedUserId,
            dbContext,
            cancellationToken);

        var response = new ManagementContextResponse(
            ScopeConsolidated,
            null,
            availableCondominiums);

        return Results.Ok(response);
    }

    private static async Task<IResult> HandlePutAsync(
        ManagementContextRequest request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedUserId(principal, out var authenticatedUserId))
        {
            return InvalidAuthenticatedUser();
        }

        if (!await IsAuthenticatedUserActive(authenticatedUserId, dbContext, cancellationToken))
        {
            return AuthenticationFailed();
        }

        if (string.IsNullOrWhiteSpace(request.Scope))
        {
            return Results.BadRequest(new { error = "Scope is required." });
        }

        if (request.Scope is not ScopeConsolidated and not ScopeCondominium)
        {
            return Results.BadRequest(new
            {
                error = "Scope must be either 'Consolidated' or 'Condominium'."
            });
        }

        var availableCondominiums = await GetManagementCondominiumsAsync(
            authenticatedUserId,
            dbContext,
            cancellationToken);

        if (request.Scope == ScopeCondominium)
        {
            if (request.CondominiumId is null || request.CondominiumId == Guid.Empty)
            {
                return Results.BadRequest(new
                {
                    error = "CondominiumId is required when scope is 'Condominium'."
                });
            }

            if (!availableCondominiums.Any(item =>
                    item.Id == request.CondominiumId.Value))
            {
                return Results.Forbid();
            }

            return Results.Ok(new ManagementContextResponse(
                request.Scope,
                request.CondominiumId,
                availableCondominiums));
        }

        return Results.Ok(new ManagementContextResponse(
            request.Scope,
            null,
            availableCondominiums));
    }

    private static async Task<bool> IsAuthenticatedUserActive(
        Guid userId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .AnyAsync(user => user.Id == userId && user.IsActive, cancellationToken);
    }

    private static async Task<ManagementCondominiumResponse[]> GetManagementCondominiumsAsync(
        Guid userId,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from membership in dbContext.CondominiumMemberships.AsNoTracking()
                join role in dbContext.CondominiumMembershipRoles.AsNoTracking()
                    on membership.Id equals role.CondominiumMembershipId
                join condominium in dbContext.Condominiums.AsNoTracking()
                    on membership.CondominiumId equals condominium.Id
                where membership.UserId == userId
                    && membership.IsActive
                    && membership.EndedAt == null
                    && role.IsActive
                    && role.RevokedAt == null
                    && role.Role == CondominiumRole.Manager
                    && condominium.IsActive
                select new
                {
                    condominium.Id,
                    condominium.Name,
                    condominium.IsActive
                })
            .Distinct()
            .OrderBy(item => item.Name)
            .ToArrayAsync(cancellationToken);

        return rows
            .Select(item => new ManagementCondominiumResponse(
                item.Id,
                item.Name,
                item.IsActive))
            .ToArray();
    }

    private static bool TryGetAuthenticatedUserId(
        ClaimsPrincipal principal,
        out Guid userId)
    {
        var authenticatedUserIdValue =
            principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(authenticatedUserIdValue, out userId);
    }

    private static IResult InvalidAuthenticatedUser() =>
        Results.Json(
            new { error = "Invalid authenticated user." },
            statusCode: StatusCodes.Status401Unauthorized);

    private static IResult AuthenticationFailed() =>
        Results.Json(
            new { error = "Authenticated user was not found or is inactive." },
            statusCode: StatusCodes.Status401Unauthorized);

    public sealed record ManagementContextRequest(
        string? Scope,
        Guid? CondominiumId);

    public sealed record ManagementCondominiumResponse(
        Guid Id,
        string Name,
        bool IsActive);

    public sealed record ManagementContextResponse(
        string Scope,
        Guid? ActiveCondominiumId,
        IReadOnlyList<ManagementCondominiumResponse> AvailableCondominiums);

    private const string ScopeConsolidated = "Consolidated";
    private const string ScopeCondominium = "Condominium";
}
