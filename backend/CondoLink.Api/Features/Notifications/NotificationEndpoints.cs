using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Notifications;

/// <summary>
/// In-app notification centre. Every route is scoped to the authenticated
/// recipient, so one user can never read or mutate another's notifications.
/// </summary>
public static class NotificationEndpoints
{
    private const int DefaultPageSize = 30;
    private const int MaximumPageSize = 100;

    public static IEndpointRouteBuilder MapNotifications(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/notifications", ListAsync)
            .RequireAuthorization()
            .WithTags("Notifications")
            .WithSummary("List notifications for the authenticated user");

        endpoints.MapGet("/notifications/unread-count", UnreadCountAsync)
            .RequireAuthorization()
            .WithTags("Notifications");

        endpoints.MapPatch("/notifications/{id:guid}/read", MarkReadAsync)
            .RequireAuthorization()
            .WithTags("Notifications");

        endpoints.MapPatch("/notifications/read-all", MarkAllReadAsync)
            .RequireAuthorization()
            .WithTags("Notifications");

        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        Guid? condominiumId,
        bool? unreadOnly,
        int? take,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var caller = await ResolveCallerAsync(principal, dbContext, cancellationToken);
        if (caller.Error is not null) return caller.Error;

        var pageSize = Math.Clamp(take ?? DefaultPageSize, 1, MaximumPageSize);

        var query = dbContext.Notifications
            .AsNoTracking()
            .Where(notification => notification.RecipientUserId == caller.UserId);

        if (condominiumId.HasValue)
        {
            query = query.Where(notification => notification.CondominiumId == condominiumId.Value);
        }

        if (unreadOnly == true)
        {
            query = query.Where(notification => notification.ReadAt == null);
        }

        var items = await query
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(pageSize)
            .Select(notification => new NotificationResponse(
                notification.Id,
                notification.CondominiumId,
                notification.Type.ToString(),
                notification.Title,
                notification.Body,
                notification.RequestId,
                notification.CreatedAt,
                notification.ReadAt))
            .ToListAsync(cancellationToken);

        var unread = await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.RecipientUserId == caller.UserId
                    && notification.ReadAt == null,
                cancellationToken);

        return Results.Ok(new ListResponse(items, unread));
    }

    private static async Task<IResult> UnreadCountAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var caller = await ResolveCallerAsync(principal, dbContext, cancellationToken);
        if (caller.Error is not null) return caller.Error;

        var unread = await dbContext.Notifications
            .AsNoTracking()
            .CountAsync(
                notification => notification.RecipientUserId == caller.UserId
                    && notification.ReadAt == null,
                cancellationToken);

        return Results.Ok(new UnreadCountResponse(unread));
    }

    private static async Task<IResult> MarkReadAsync(
        Guid id,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var caller = await ResolveCallerAsync(principal, dbContext, cancellationToken);
        if (caller.Error is not null) return caller.Error;

        var notification = await dbContext.Notifications
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        // Scoped by recipient: another user's notification is reported as
        // missing rather than forbidden, so ids cannot be probed.
        if (notification is null || notification.RecipientUserId != caller.UserId)
        {
            return Results.NotFound(new { error = "Notification not found." });
        }

        notification.MarkAsRead(DateTime.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.NoContent();
    }

    private static async Task<IResult> MarkAllReadAsync(
        Guid? condominiumId,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var caller = await ResolveCallerAsync(principal, dbContext, cancellationToken);
        if (caller.Error is not null) return caller.Error;

        var pending = dbContext.Notifications
            .Where(notification => notification.RecipientUserId == caller.UserId
                && notification.ReadAt == null);

        if (condominiumId.HasValue)
        {
            pending = pending.Where(
                notification => notification.CondominiumId == condominiumId.Value);
        }

        var readAt = DateTime.UtcNow;
        var affected = await pending.ToListAsync(cancellationToken);
        foreach (var notification in affected)
        {
            notification.MarkAsRead(readAt);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return Results.Ok(new MarkAllReadResponse(affected.Count));
    }

    private static async Task<Caller> ResolveCallerAsync(
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(value, out var userId))
        {
            return new Caller(Guid.Empty, Results.Json(
                new { error = "Invalid authenticated user." },
                statusCode: StatusCodes.Status401Unauthorized));
        }

        var user = await dbContext.Set<ApplicationUser>()
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return new Caller(userId, Results.Json(
                new { error = "Authenticated user was not found." },
                statusCode: StatusCodes.Status401Unauthorized));
        }

        if (!user.IsActive)
        {
            return new Caller(userId, Results.Json(
                new { error = "User account is inactive." },
                statusCode: StatusCodes.Status403Forbidden));
        }

        return new Caller(userId, null);
    }

    private sealed record Caller(Guid UserId, IResult? Error);

    public sealed record NotificationResponse(
        Guid Id,
        Guid CondominiumId,
        string Type,
        string Title,
        string Body,
        Guid? RequestId,
        DateTime CreatedAt,
        DateTime? ReadAt);

    public sealed record ListResponse(
        IReadOnlyList<NotificationResponse> Items,
        int UnreadCount);

    public sealed record UnreadCountResponse(int UnreadCount);

    public sealed record MarkAllReadResponse(int Updated);
}
