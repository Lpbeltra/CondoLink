using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class UpdateRequestPriority
{
    public static IEndpointRouteBuilder MapUpdateRequestPriority(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/requests/{requestId:guid}/priority", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid requestId,
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

        var targetRequest = await dbContext.Requests
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);

        if (targetRequest is null)
        {
            return Results.NotFound(new { error = "Request not found." });
        }

        var isCondominiumManager = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == authenticatedUserId
                && membership.CondominiumId == targetRequest.CondominiumId
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
                new { error = "Only condominium managers can update requests." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!TryParsePriority(request.Priority, out var newPriority))
        {
            return Results.BadRequest(new { error = "Invalid request priority." });
        }

        if (targetRequest.Status == RequestStatus.Cancelled)
        {
            return Results.Conflict(new
            {
                error = "Cancelled requests cannot have their priority changed."
            });
        }

        if (targetRequest.Priority == newPriority)
        {
            return Results.Conflict(
                new { error = "Request already has this priority." });
        }

        targetRequest.ChangePriority(newPriority, DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new Response(
            targetRequest.Id,
            targetRequest.Status.ToString(),
            targetRequest.Priority.ToString(),
            targetRequest.UpdatedAt,
            targetRequest.ResolvedAt));
    }

    private static bool TryParsePriority(string? value, out RequestPriority priority)
    {
        priority = default;

        return !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse(value, ignoreCase: true, out priority)
            && Enum.IsDefined(priority);
    }

    public sealed record RequestDto(string? Priority);

    public sealed record Response(
        Guid Id,
        string Status,
        string Priority,
        DateTime UpdatedAt,
        DateTime? ResolvedAt);
}
