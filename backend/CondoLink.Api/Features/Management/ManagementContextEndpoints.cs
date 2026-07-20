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

        var user = await dbContext.Set<ApplicationUser>()
            .SingleOrDefaultAsync(
                item => item.Id == authenticatedUserId && item.IsActive,
                cancellationToken);

        if (user is null)
        {
            return AuthenticationFailed();
        }

        var availableCondominiums = await GetManagementCondominiumsAsync(
            authenticatedUserId,
            dbContext,
            cancellationToken);

        var activeCondominiumId =
            user.ActiveManagementCondominiumId is Guid storedCondominiumId
            && availableCondominiums.Any(item => item.Id == storedCondominiumId)
                ? storedCondominiumId
                : (Guid?)null;

        // Se existe apenas um condomínio disponível, seleciona automaticamente.
        if (activeCondominiumId is null &&
            availableCondominiums.Length == 1)
        {
            activeCondominiumId = availableCondominiums[0].Id;

            user.SetActiveManagementCondominium(
                activeCondominiumId.Value);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(new ManagementContextResponse(
            activeCondominiumId,
            availableCondominiums));
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

        var user = await dbContext.Set<ApplicationUser>()
            .SingleOrDefaultAsync(
                item => item.Id == authenticatedUserId && item.IsActive,
                cancellationToken);

        if (user is null)
        {
            return AuthenticationFailed();
        }

        if (request.CondominiumId is null ||
            request.CondominiumId == Guid.Empty)
        {
            return Results.BadRequest(new
            {
                error = "CondominiumId is required."
            });
        }

        var availableCondominiums = await GetManagementCondominiumsAsync(
            authenticatedUserId,
            dbContext,
            cancellationToken);

        var selectedCondominiumIsAvailable =
            availableCondominiums.Any(item =>
                item.Id == request.CondominiumId.Value);

        if (!selectedCondominiumIsAvailable)
        {
            return Results.Forbid();
        }

        user.SetActiveManagementCondominium(
            request.CondominiumId.Value);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new ManagementContextResponse(
            user.ActiveManagementCondominiumId,
            availableCondominiums));
    }

    private static async Task<ManagementCondominiumResponse[]>
        GetManagementCondominiumsAsync(
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
        Guid? CondominiumId);

    public sealed record ManagementCondominiumResponse(
        Guid Id,
        string Name,
        bool IsActive);

    public sealed record ManagementContextResponse(
        Guid? ActiveCondominiumId,
        IReadOnlyList<ManagementCondominiumResponse> AvailableCondominiums);
}