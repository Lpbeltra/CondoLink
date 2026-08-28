using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Common;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Api.Features.Requests;

public static class ListCondominiumRequests
{
    public static IEndpointRouteBuilder MapListCondominiumRequests(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/management/requests",
                HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private const int DefaultPageSize = 200;
    private const int MaximumPageSize = 500;

    private static async Task<IResult> HandleAsync(
        string? status,
        string? priority,
        Guid? categoryId,
        Guid? targetUnitId,
        Guid? authorUserId,
        Guid? condominiumId,
        string? search,
        int? page,
        int? pageSize,
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

        var condominiumRequests = AuthorizedRequests(dbContext, authenticatedUserId);

        if (condominiumId.HasValue)
        {
            var canManageCondominium = await condominiumRequests
                .AnyAsync(
                    request => request.CondominiumId == condominiumId.Value,
                    cancellationToken);
            if (!canManageCondominium)
            {
                var hasManagerAccess = await (
                        from membership in dbContext.CondominiumMemberships
                            .AsNoTracking()
                        join role in dbContext.CondominiumMembershipRoles
                            .AsNoTracking()
                            on membership.Id equals role.CondominiumMembershipId
                        join condominium in dbContext.Condominiums.AsNoTracking()
                            on membership.CondominiumId equals condominium.Id
                        where membership.UserId == authenticatedUserId
                            && membership.CondominiumId == condominiumId.Value
                            && membership.IsActive
                            && membership.EndedAt == null
                            && (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager)
                            && role.IsActive
                            && role.RevokedAt == null
                            && condominium.IsActive
                        select membership.Id)
                    .AnyAsync(cancellationToken);
                if (!hasManagerAccess)
                {
                    return Results.Forbid();
                }
            }

            condominiumRequests = condominiumRequests.Where(
                request => request.CondominiumId == condominiumId.Value);
        }

        var statusCounts = await condominiumRequests.GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);
        var counts = new CountsResponse(
            statusCounts.GetValueOrDefault(RequestStatus.Open),
            statusCounts.GetValueOrDefault(RequestStatus.InProgress),
            statusCounts.GetValueOrDefault(RequestStatus.WaitingForResident),
            statusCounts.GetValueOrDefault(RequestStatus.WaitingForManager),
            statusCounts.GetValueOrDefault(RequestStatus.WaitingForThirdParty),
            statusCounts.GetValueOrDefault(RequestStatus.WaitingForResidentClosure),
            statusCounts.GetValueOrDefault(RequestStatus.Resolved),
            statusCounts.GetValueOrDefault(RequestStatus.Cancelled));

        var requests = condominiumRequests;

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

        var projected =
            from request in requests
            join author in dbContext.Set<ApplicationUser>().AsNoTracking()
                on request.AuthorUserId equals author.Id
            join category in dbContext.Categories.AsNoTracking()
                on request.CategoryId equals category.Id
            join condominium in dbContext.Condominiums.AsNoTracking()
                on request.CondominiumId equals condominium.Id
            join unit in dbContext.Units.AsNoTracking()
                on request.TargetUnitId equals unit.Id into targetUnits
            from unit in targetUnits.DefaultIfEmpty()
            select new
            {
                request.Id,
                request.CondominiumId,
                CondominiumName = condominium.Name,
                AuthorId = author.Id,
                AuthorFullName = author.FullName,
                CategoryId = category.Id,
                CategoryName = category.Name,
                TargetUnitId = unit == null ? (Guid?)null : unit.Id,
                TargetUnitIdentifier = unit == null ? null : unit.Identifier,
                TargetUnitBlock = unit == null ? null : dbContext.CondominiumBlocks.Where(block => block.Id == unit.BlockId).Select(block => block.Identifier).FirstOrDefault(),
                request.Title,
                request.Status,
                request.Priority,
                request.CreatedAt,
                request.UpdatedAt,
                request.ResolvedAt
                ,HasUnreadResidentReply = dbContext.RequestResidentReplyRequirements
                    .Any(requirement => requirement.RequestId == request.Id
                        && requirement.HasUnreadAnswer)
                ,HasUnreadResidentUpdate = dbContext.Notifications
                    .Any(notification => notification.RequestId == request.Id
                        && notification.RecipientUserId == authenticatedUserId
                        && notification.Type == NotificationType.ResidentRequestUpdated
                        && notification.ReadAt == null)
            };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            projected = projected.Where(row =>
                row.Title.ToLower().Contains(term)
                || row.AuthorFullName.ToLower().Contains(term)
                || row.CategoryName.ToLower().Contains(term)
                || (row.TargetUnitIdentifier != null && row.TargetUnitIdentifier.ToLower().Contains(term))
                || (row.TargetUnitBlock != null && row.TargetUnitBlock.ToLower().Contains(term)));
        }

        var (normalizedPage, normalizedPageSize) =
            PagedResult.Normalize(page, pageSize, DefaultPageSize, MaximumPageSize);

        var total = await projected.CountAsync(cancellationToken);

        var rows = await projected
            .OrderBy(row => row.Status == RequestStatus.Resolved || row.Status == RequestStatus.Cancelled)
            .ThenByDescending(row => row.Priority)
            .ThenByDescending(row => row.UpdatedAt)
            .ThenByDescending(row => row.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(item => new ItemResponse(
                item.Id,
                item.CondominiumId,
                item.CondominiumName,
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
                item.ResolvedAt,
                item.HasUnreadResidentReply,
                item.HasUnreadResidentUpdate))
            .ToArray();

        return Results.Ok(new Response(total, normalizedPage, normalizedPageSize, counts, items));
    }

    public static IQueryable<DomainRequest> AuthorizedRequests(AppDbContext dbContext, Guid managerUserId)
    {
        var managedCondominiumIds = dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == managerUserId && membership.IsActive && membership.EndedAt == null)
            .Join(dbContext.CondominiumMembershipRoles.AsNoTracking().Where(role =>
                    (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager) && role.IsActive && role.RevokedAt == null),
                membership => membership.Id, role => role.CondominiumMembershipId,
                (membership, _) => membership.CondominiumId)
            .Join(
                dbContext.Condominiums.AsNoTracking().Where(
                    condominium => condominium.IsActive),
                condominiumId => condominiumId,
                condominium => condominium.Id,
                (condominiumId, _) => condominiumId)
            .Distinct();
        return dbContext.Requests.AsNoTracking().Where(request => managedCondominiumIds.Contains(request.CondominiumId));
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
        int WaitingForManager,
        int WaitingForThirdParty,
        int WaitingForResidentClosure,
        int Resolved,
        int Cancelled);

    public sealed record ItemResponse(
        Guid Id,
        Guid CondominiumId,
        string CondominiumName,
        AuthorResponse Author,
        CategoryResponse Category,
        TargetUnitResponse? TargetUnit,
        string Title,
        string Status,
        string Priority,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        DateTime? ResolvedAt,
        bool HasUnreadResidentReply,
        bool HasUnreadResidentUpdate)
    {
        public string Protocol => RequestProtocol.From(Id);
    }

    public sealed record Response(
        int Total,
        int Page,
        int PageSize,
        CountsResponse Counts,
        IReadOnlyList<ItemResponse> Items);
}
