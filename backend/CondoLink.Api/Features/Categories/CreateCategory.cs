using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using CondoLink.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CondoLink.Api.Features.Categories;

public static class CreateCategory
{
    public static IEndpointRouteBuilder MapCreateCategory(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/condominiums/{condominiumId:guid}/categories",
                HandleAsync)
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
                new { error = "Only condominium managers can create categories." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!condominium.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive condominium cannot receive new categories." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.BadRequest(new { error = "Name is required." });
        }

        var name = request.Name.Trim();
        var description = request.Description?.Trim();

        if (name.Length > 100)
        {
            return Results.BadRequest(
                new { error = "Name must not exceed 100 characters." });
        }

        if (description?.Length > 500)
        {
            return Results.BadRequest(
                new { error = "Description must not exceed 500 characters." });
        }

        var normalizedName = name.ToUpperInvariant();

        var alreadyExists = await dbContext.Categories
            .AsNoTracking()
            .AnyAsync(
                category =>
                    category.CondominiumId == condominiumId
                    && category.NormalizedName == normalizedName,
                cancellationToken);

        if (alreadyExists)
        {
            return DuplicateCategoryConflict();
        }

        var category = new Category(condominiumId, name, description);
        dbContext.Categories.Add(category);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsDuplicateCategoryViolation(exception))
        {
            return DuplicateCategoryConflict();
        }

        var response = new Response(
            category.Id,
            category.CondominiumId,
            category.Name,
            category.Description,
            category.IsActive,
            category.CreatedAt,
            category.UpdatedAt);

        return Results.Created($"/categories/{category.Id}", response);
    }

    private static bool IsDuplicateCategoryViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName:
                CategoryConfiguration.UniqueCondominiumNormalizedNameIndex
        };
    }

    private static IResult DuplicateCategoryConflict()
    {
        return Results.Conflict(new
        {
            error = "A category with this name already exists in the condominium."
        });
    }

    public sealed record Request(string? Name, string? Description);

    public sealed record Response(
        Guid Id,
        Guid CondominiumId,
        string Name,
        string? Description,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
