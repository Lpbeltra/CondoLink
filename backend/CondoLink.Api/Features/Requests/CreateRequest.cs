using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Api.Features.Requests;

public static class CreateRequest
{
    public static IEndpointRouteBuilder MapCreateRequest(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/condominiums/{condominiumId:guid}/requests",
                HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        RequestDto request,
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
                new { error = "Only active condominium members can create requests." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!condominium.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive condominium cannot receive new requests." });
        }

        if (request.CategoryId == Guid.Empty)
        {
            return Results.BadRequest(new { error = "Category is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Results.BadRequest(new { error = "Title is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return Results.BadRequest(new { error = "Description is required." });
        }

        var title = request.Title.Trim();
        var description = request.Description.Trim();

        if (title.Length > 200)
        {
            return Results.BadRequest(
                new { error = "Title must not exceed 200 characters." });
        }

        if (description.Length > 4000)
        {
            return Results.BadRequest(
                new { error = "Description must not exceed 4000 characters." });
        }

        var category = await dbContext.Categories
            .AsNoTracking()
            .Where(category => category.Id == request.CategoryId)
            .Select(category => new
            {
                category.CondominiumId,
                category.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (category is null)
        {
            return Results.NotFound(new { error = "Category not found." });
        }

        if (category.CondominiumId != condominiumId)
        {
            return Results.BadRequest(new
            {
                error = "Category must belong to the request condominium."
            });
        }

        if (!category.IsActive)
        {
            return Results.Conflict(
                new { error = "Inactive category cannot be used in new requests." });
        }

        if (request.TargetUnitId.HasValue)
        {
            if (request.TargetUnitId == Guid.Empty)
            {
                return Results.BadRequest(new { error = "Target unit is invalid." });
            }

            var targetUnit = await dbContext.Units
                .AsNoTracking()
                .Where(unit => unit.Id == request.TargetUnitId)
                .Select(unit => new { unit.CondominiumId })
                .SingleOrDefaultAsync(cancellationToken);

            if (targetUnit is null)
            {
                return Results.NotFound(new { error = "Target unit not found." });
            }

            if (targetUnit.CondominiumId != condominiumId)
            {
                return Results.BadRequest(new
                {
                    error = "Target unit must belong to the request condominium."
                });
            }
        }

        var domainRequest = new DomainRequest(
            condominiumId,
            authenticatedUserId,
            request.TargetUnitId,
            request.CategoryId,
            title,
            description);

        var initialHistory = new RequestStatusHistory(
            domainRequest.Id,
            previousStatus: null,
            RequestStatus.Open,
            authenticatedUserId,
            reason: null,
            domainRequest.CreatedAt);

        dbContext.Requests.Add(domainRequest);
        dbContext.RequestStatusHistories.Add(initialHistory);
        await dbContext.SaveChangesAsync(cancellationToken);

        var response = new Response(
            domainRequest.Id,
            domainRequest.CondominiumId,
            domainRequest.AuthorUserId,
            domainRequest.TargetUnitId,
            domainRequest.CategoryId,
            domainRequest.Title,
            domainRequest.Description,
            domainRequest.Status.ToString(),
            domainRequest.Priority.ToString(),
            domainRequest.CreatedAt,
            domainRequest.UpdatedAt,
            domainRequest.ResolvedAt);

        return Results.Created($"/requests/{domainRequest.Id}", response);
    }

    public sealed record RequestDto(
        Guid CategoryId,
        Guid? TargetUnitId,
        string? Title,
        string? Description);

    public sealed record Response(
        Guid Id,
        Guid CondominiumId,
        Guid AuthorUserId,
        Guid? TargetUnitId,
        Guid CategoryId,
        string Title,
        string Description,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? ResolvedAt);
}
