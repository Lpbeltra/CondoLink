using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class ListCondominiumRequests
{
    public static IEndpointRouteBuilder MapListCondominiumRequests(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/condominiums/{condominiumId:guid}/requests",
                HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId,
        string? status,
        string? priority,
        Guid? categoryId,
        Guid? targetUnitId,
        Guid? authorUserId,
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
                new { error = "Only condominium managers can view condominium requests." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        RequestStatus? statusFilter = null;
        RequestPriority? priorityFilter = null;

        if (status is not null)
        {
            if (!TryParseStatus(status, out var parsedStatus))
            {
                return Results.BadRequest(
                    new { error = "Invalid request status filter." });
            }

            statusFilter = parsedStatus;
        }

        if (priority is not null)
        {
            if (!TryParsePriority(priority, out var parsedPriority))
            {
                return Results.BadRequest(
                    new { error = "Invalid request priority filter." });
            }

            priorityFilter = parsedPriority;
        }

        var requests = dbContext.Requests
            .AsNoTracking()
            .Where(request => request.CondominiumId == condominiumId);

        if (statusFilter.HasValue)
        {
            requests = requests.Where(request => request.Status == statusFilter.Value);
        }

        if (priorityFilter.HasValue)
        {
            requests = requests.Where(request => request.Priority == priorityFilter.Value);
        }

        if (categoryId.HasValue)
        {
            requests = requests.Where(request => request.CategoryId == categoryId.Value);
        }

        if (targetUnitId.HasValue)
        {
            requests = requests.Where(request => request.TargetUnitId == targetUnitId.Value);
        }

        if (authorUserId.HasValue)
        {
            requests = requests.Where(request => request.AuthorUserId == authorUserId.Value);
        }

        var rows = await (
                from request in requests
                join author in dbContext.Set<ApplicationUser>().AsNoTracking()
                    on request.AuthorUserId equals author.Id
                join category in dbContext.Categories.AsNoTracking()
                    on request.CategoryId equals category.Id
                join unit in dbContext.Units.AsNoTracking()
                    on request.TargetUnitId equals unit.Id into targetUnits
                from unit in targetUnits.DefaultIfEmpty()
                orderby request.Status == RequestStatus.Resolved
                        || request.Status == RequestStatus.Cancelled,
                    request.Priority descending,
                    request.UpdatedAt descending,
                    request.Id descending
                select new
                {
                    request.Id,
                    request.CondominiumId,
                    AuthorId = author.Id,
                    AuthorFullName = author.FullName,
                    CategoryId = category.Id,
                    CategoryName = category.Name,
                    TargetUnitId = unit == null ? (Guid?)null : unit.Id,
                    TargetUnitIdentifier = unit == null ? null : unit.Identifier,
                    TargetUnitBlock = unit == null ? null : unit.Block,
                    request.Title,
                    request.Status,
                    request.Priority,
                    request.CreatedAt,
                    request.UpdatedAt,
                    request.ResolvedAt
                })
            .ToListAsync(cancellationToken);

        var counts = new CountsResponse(
            rows.Count(item => item.Status == RequestStatus.Open),
            rows.Count(item => item.Status == RequestStatus.InProgress),
            rows.Count(item => item.Status == RequestStatus.WaitingForResident),
            rows.Count(item => item.Status == RequestStatus.WaitingForThirdParty),
            rows.Count(item => item.Status == RequestStatus.Resolved),
            rows.Count(item => item.Status == RequestStatus.Cancelled));

        var items = rows
            .Select(item => new ItemResponse(
                item.Id,
                item.CondominiumId,
                new AuthorResponse(item.AuthorId, item.AuthorFullName),
                new CategoryResponse(item.CategoryId, item.CategoryName),
                item.TargetUnitId.HasValue
                    ? new TargetUnitResponse(
                        item.TargetUnitId.Value,
                        item.TargetUnitIdentifier!,
                        item.TargetUnitBlock)
                    : null,
                item.Title,
                item.Status.ToString(),
                item.Priority.ToString(),
                item.CreatedAt,
                item.UpdatedAt,
                item.ResolvedAt))
            .ToArray();

        return Results.Ok(new Response(rows.Count, counts, items));
    }

    private static bool TryParseStatus(string value, out RequestStatus status)
    {
        status = default;

        return !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse(value, ignoreCase: true, out status)
            && Enum.IsDefined(status);
    }

    private static bool TryParsePriority(
        string value,
        out RequestPriority priority)
    {
        priority = default;

        return !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse(value, ignoreCase: true, out priority)
            && Enum.IsDefined(priority);
    }

    public sealed record AuthorResponse(Guid Id, string FullName);
    public sealed record CategoryResponse(Guid Id, string Name);
    public sealed record TargetUnitResponse(Guid Id, string Identifier, string? Block);

    public sealed record CountsResponse(
        int Open,
        int InProgress,
        int WaitingForResident,
        int WaitingForThirdParty,
        int Resolved,
        int Cancelled);

    public sealed record ItemResponse(
        Guid Id,
        Guid CondominiumId,
        AuthorResponse Author,
        CategoryResponse Category,
        TargetUnitResponse? TargetUnit,
        string Title,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? ResolvedAt);

    public sealed record Response(
        int Total,
        CountsResponse Counts,
        IReadOnlyList<ItemResponse> Items);
}
