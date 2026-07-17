using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.RequestMessages;

public static class CreateRequestMessage
{
    public static IEndpointRouteBuilder MapCreateRequestMessage(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/requests/{requestId:guid}/messages", HandleAsync)
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
            .Select(user => new { user.IsActive, user.FullName })
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
            .Select(item => new
            {
                item.AuthorUserId,
                item.CondominiumId,
                item.Status
            })
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

        if (targetRequest.Status == RequestStatus.Cancelled)
        {
            return Results.Conflict(
                new { error = "Cancelled requests cannot receive new messages." });
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest(new { error = "Content is required." });
        }

        var content = request.Content.Trim();

        if (content.Length > 4000)
        {
            return Results.BadRequest(
                new { error = "Content must not exceed 4000 characters." });
        }

        var message = new RequestMessage(requestId, authenticatedUserId, content);
        dbContext.RequestMessages.Add(message);
        await dbContext.SaveChangesAsync(cancellationToken);

        var authorIsManager = await dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.UserId == authenticatedUserId
                && membership.CondominiumId == targetRequest.CondominiumId
                && membership.IsActive
                && membership.EndedAt == null)
            .Join(
                dbContext.CondominiumMembershipRoles.AsNoTracking().Where(role =>
                    role.Role == CondominiumRole.Manager
                    && role.IsActive
                    && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (_, _) => true)
            .AnyAsync(cancellationToken);

        var response = new Response(
            message.Id,
            message.RequestId,
            new AuthorResponse(message.AuthorUserId, authenticatedUser.FullName, authorIsManager),
            message.Content,
            message.CreatedAt);

        return Results.Created($"/request-messages/{message.Id}", response);
    }

    public sealed record RequestDto(string? Content);
    public sealed record AuthorResponse(Guid Id, string FullName, bool IsManager);

    public sealed record Response(
        Guid Id,
        Guid RequestId,
        AuthorResponse Author,
        string Content,
        DateTime CreatedAt);
}
