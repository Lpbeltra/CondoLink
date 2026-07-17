using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.RequestMessages;

public static class ListRequestMessages
{
    public static IEndpointRouteBuilder MapListRequestMessages(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/requests/{requestId:guid}/messages", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid requestId,
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
            .AsNoTracking()
            .Where(item => item.Id == requestId)
            .Select(item => new { item.AuthorUserId, item.CondominiumId })
            .SingleOrDefaultAsync(cancellationToken);

        if (targetRequest is null)
        {
            return Results.NotFound(new { error = "Request not found." });
        }

        if (targetRequest.AuthorUserId != authenticatedUserId)
        {
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
                    new { error = "You do not have access to this request." },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        var rows = await dbContext.RequestMessages
            .AsNoTracking()
            .Where(message => message.RequestId == requestId)
            .Join(
                dbContext.Set<ApplicationUser>().AsNoTracking(),
                message => message.AuthorUserId,
                user => user.Id,
                (message, user) => new
                {
                    message.Id,
                    message.RequestId,
                    AuthorUserId = user.Id,
                    AuthorFullName = user.FullName,
                    message.Content,
                    message.CreatedAt
                })
            .OrderBy(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .ToListAsync(cancellationToken);

        var managerUserIds = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.CondominiumId == targetRequest.CondominiumId
                && membership.IsActive
                && membership.EndedAt == null)
            .Join(
                dbContext.CondominiumMembershipRoles.AsNoTracking().Where(role =>
                    role.Role == CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (membership, _) => membership.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var messages = rows
            .Select(message => new Response(
                message.Id,
                message.RequestId,
                new AuthorResponse(
                    message.AuthorUserId,
                    message.AuthorFullName,
                    managerUserIds.Contains(message.AuthorUserId)),
                message.Content,
                message.CreatedAt))
            .ToArray();

        return Results.Ok(messages);
    }

    public sealed record AuthorResponse(Guid Id, string FullName, bool IsManager);

    public sealed record Response(
        Guid Id,
        Guid RequestId,
        AuthorResponse Author,
        string Content,
        DateTime CreatedAt);
}
