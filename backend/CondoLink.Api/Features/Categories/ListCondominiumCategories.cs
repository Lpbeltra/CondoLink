using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Categories;

public static class ListCondominiumCategories
{
    public static IEndpointRouteBuilder MapListCondominiumCategories(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/condominiums/{condominiumId:guid}/categories",
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

        var condominium = await dbContext.Condominiums
            .AsNoTracking()
            .Where(condominium => condominium.Id == condominiumId)
            .Select(condominium => new { condominium.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (condominium is null)
        {
            return Results.NotFound(new { error = "Condominium not found." });
        }

        var isActiveCondominiumMember = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .AnyAsync(
                membership =>
                    membership.UserId == authenticatedUserId
                    && membership.CondominiumId == condominiumId
                    && membership.IsActive
                    && membership.EndedAt == null,
                cancellationToken);

        if (!isActiveCondominiumMember)
        {
            return Results.Json(
                new { error = "Only active condominium members can view categories." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!condominium.IsActive)
        {
            return Results.Conflict(new
            {
                error = "Inactive condominium does not provide categories for new requests."
            });
        }

        var categories = await dbContext.Categories
            .AsNoTracking()
            .Where(category =>
                category.CondominiumId == condominiumId
                && category.IsActive)
            .OrderBy(category => category.Name)
            .Select(category => new Response(
                category.Id,
                category.CondominiumId,
                category.Name,
                category.Description,
                dbContext.Requests.Count(request => request.CategoryId == category.Id)))
            .ToListAsync(cancellationToken);

        return Results.Ok(categories);
    }

    public sealed record Response(
        Guid Id,
        Guid CondominiumId,
        string Name,
        string? Description,
        int RequestCount);
}
