using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class GetRequestById
{
    public static IEndpointRouteBuilder MapGetRequestById(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/requests/{id:guid}", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid id,
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

        var request = await dbContext.Requests
            .AsNoTracking()
            .Where(request => request.Id == id)
            .Join(
                dbContext.Set<ApplicationUser>().AsNoTracking(),
                request => request.AuthorUserId,
                author => author.Id,
                (request, author) => new { request, author })
            .Join(
                dbContext.Categories.AsNoTracking(),
                item => item.request.CategoryId,
                category => category.Id,
                (item, category) => new
                {
                    item.request.Id,
                    item.request.CondominiumId,
                    item.request.AuthorUserId,
                    AuthorFullName = item.author.FullName,
                    item.request.TargetUnitId,
                    item.request.CategoryId,
                    CategoryName = category.Name,
                    item.request.Title,
                    item.request.Description,
                    item.request.Status,
                    item.request.Priority,
                    item.request.CreatedAt,
                    item.request.UpdatedAt,
                    item.request.ResolvedAt
                })
            .SingleOrDefaultAsync(cancellationToken);

        if (request is null)
        {
            return Results.NotFound(new { error = "Request not found." });
        }

        if (request.AuthorUserId != authenticatedUserId)
        {
            var isCondominiumManager = await dbContext.CondominiumMemberships
                .AsNoTracking()
                .Where(membership =>
                    membership.UserId == authenticatedUserId
                    && membership.CondominiumId == request.CondominiumId
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
                    new { error = "You do not have access to this request." },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        TargetUnitResponse? targetUnit = null;

        if (request.TargetUnitId.HasValue)
        {
            targetUnit = await dbContext.Units
                .AsNoTracking()
                .Where(unit => unit.Id == request.TargetUnitId)
                .Select(unit => new TargetUnitResponse(
                    unit.Id,
                    unit.Identifier,
                    unit.Block))
                .SingleOrDefaultAsync(cancellationToken);
        }

        var historyRows = await dbContext.RequestStatusHistories
            .AsNoTracking()
            .Where(history => history.RequestId == id)
            .Join(
                dbContext.Set<ApplicationUser>().AsNoTracking(),
                history => history.ChangedByUserId,
                user => user.Id,
                (history, user) => new
                {
                    history.Id,
                    history.PreviousStatus,
                    history.NewStatus,
                    history.ChangedByUserId,
                    ChangedByFullName = user.FullName,
                    history.Reason,
                    history.CreatedAt
                })
            .OrderBy(history => history.CreatedAt)
            .ThenBy(history => history.Id)
            .ToListAsync(cancellationToken);

        var statusHistory = historyRows
            .Select(history => new StatusHistoryResponse(
                history.Id,
                history.PreviousStatus?.ToString(),
                history.NewStatus.ToString(),
                history.ChangedByUserId,
                history.ChangedByFullName,
                history.Reason,
                history.CreatedAt))
            .ToArray();

        var response = new Response(
            request.Id,
            request.CondominiumId,
            new AuthorResponse(request.AuthorUserId, request.AuthorFullName),
            targetUnit,
            new CategoryResponse(request.CategoryId, request.CategoryName),
            request.Title,
            request.Description,
            request.Status.ToString(),
            request.Priority.ToString(),
            request.CreatedAt,
            request.UpdatedAt,
            request.ResolvedAt,
            statusHistory);

        return Results.Ok(response);
    }

    public sealed record AuthorResponse(Guid Id, string FullName);
    public sealed record TargetUnitResponse(Guid Id, string Identifier, string? Block);
    public sealed record CategoryResponse(Guid Id, string Name);

    public sealed record StatusHistoryResponse(
        Guid Id,
        string? PreviousStatus,
        string NewStatus,
        Guid ChangedByUserId,
        string ChangedByFullName,
        string? Reason,
        DateTime CreatedAt);

    public sealed record Response(
        Guid Id,
        Guid CondominiumId,
        AuthorResponse Author,
        TargetUnitResponse? TargetUnit,
        CategoryResponse Category,
        string Title,
        string Description,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? ResolvedAt,
        IReadOnlyList<StatusHistoryResponse> StatusHistory);
}
