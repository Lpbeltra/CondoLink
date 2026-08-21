using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CondoLink.Api.Features.Notifications;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.Requests;

public static class UpdateRequestStatus
{
    public static IEndpointRouteBuilder MapUpdateRequestStatus(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/requests/{requestId:guid}/status", HandleAsync)
            .RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid requestId,
        RequestDto request,
        ClaimsPrincipal principal,
        AppDbContext dbContext,
        NotificationService notifications,
        IServiceProvider services,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(typeof(UpdateRequestStatus));
        logger.LogInformation(
            "WhatsApp notification flow. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}",
            requestId, Guid.Empty, "Undetermined", "EnteredUpdateRequestStatus", "EndpointEntered");

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
            .SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);

        if (targetRequest is null)
        {
            logger.LogInformation(
                "WhatsApp notification flow. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}",
                requestId, Guid.Empty, "Undetermined", "Stopped", "RequestNotFound");
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

        if (!TryParseStatus(request.Status, out var newStatus))
        {
            return Results.BadRequest(new { error = "Invalid request status." });
        }

        var reason = request.Reason?.Trim();
        reason = string.IsNullOrEmpty(reason) ? null : reason;

        if (reason?.Length > 1000)
        {
            return Results.BadRequest(
                new { error = "A mensagem pode ter no máximo 1000 caracteres." });
        }

        if (RequiresReason(newStatus) && reason is null)
        {
            return Results.BadRequest(new
            {
                error = "A comment is required for the selected status."
            });
        }
        if (targetRequest.Status == newStatus)
        {
            return Results.Conflict(
                new { error = "Request already has this status." });
        }

        var previousStatus = targetRequest.Status;
        var changedAt = DateTime.UtcNow;

        try
        {
            targetRequest.ChangeStatus(newStatus, changedAt);
        }
        catch (InvalidOperationException)
        {
            return Results.Conflict(new
            {
                error = "This request status transition is not allowed."
            });
        }

        var history = new RequestStatusHistory(
            targetRequest.Id,
            previousStatus,
            newStatus,
            authenticatedUserId,
            reason,
            changedAt);

        if (!await TryPersistStatusChangeAsync(
                dbContext, targetRequest, previousStatus, history,
                cancellationToken))
            return Results.Conflict(new
            {
                error = "The request status changed concurrently. Try again."
            });

        // Side effect: the status change is committed, so a notification failure
        // must not turn a successful update into an error for the manager.
        try
        {
            var notificationType = NotificationService.StatusNotificationType(
                previousStatus, targetRequest.Status);
            logger.LogInformation(
                "WhatsApp notification flow. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}",
                targetRequest.Id, targetRequest.CondominiumId, notificationType,
                "CallingNotificationService", "StatusChangePersisted");
            await notifications.NotifyStatusChangedAsync(
                targetRequest, previousStatus, authenticatedUserId,
                cancellationToken, history.Id, reason);
        }
        catch (Exception)
        {
            logger.LogError(
                "WhatsApp notification flow. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}",
                targetRequest.Id, targetRequest.CondominiumId,
                NotificationService.StatusNotificationType(previousStatus, targetRequest.Status),
                "Stopped", "NotificationServiceFailed");
        }

        if (services.GetService<RequestAiAnalysisRefresher>() is { } refresher)
            await refresher.RefreshAsync(targetRequest.Id, "status_changed",
                cancellationToken);

        return Results.Ok(new Response(
            targetRequest.Id,
            targetRequest.Status.ToString(),
            targetRequest.Priority.ToString(),
            targetRequest.UpdatedAt,
            targetRequest.ResolvedAt));
    }

    private static bool TryParseStatus(string? value, out RequestStatus status)
    {
        status = default;

        return !string.IsNullOrWhiteSpace(value)
            && !int.TryParse(value, out _)
            && Enum.TryParse(value, ignoreCase: true, out status)
            && Enum.IsDefined(status);
    }

    private static bool RequiresReason(RequestStatus status) => status is
        RequestStatus.WaitingForResident
        or RequestStatus.Resolved
        or RequestStatus.WaitingForResidentClosure
        or RequestStatus.Cancelled;

    internal static async Task<bool> TryPersistStatusChangeAsync(
        AppDbContext dbContext,
        CondoLink.Domain.Entities.Request request,
        RequestStatus expectedStatus,
        RequestStatusHistory history,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(cancellationToken);
        try
        {
            var affected = await dbContext.Requests
                .Where(item => item.Id == request.Id
                    && item.Status == expectedStatus)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.Status, request.Status)
                    .SetProperty(item => item.UpdatedAt, request.UpdatedAt)
                    .SetProperty(item => item.ResolvedAt, request.ResolvedAt),
                    cancellationToken);
            if (affected != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var activeRequirement = await dbContext.RequestResidentReplyRequirements
                .SingleOrDefaultAsync(item => item.RequestId == request.Id && item.IsActive,
                    cancellationToken);
            if (request.Status == RequestStatus.WaitingForResident)
            {
                if (activeRequirement is not null)
                    throw new InvalidOperationException("The request already has an active resident reply requirement.");
                dbContext.RequestResidentReplyRequirements.Add(
                    new RequestResidentReplyRequirement(request.Id, history.ChangedByUserId,
                        history.Id, history.Reason!, history.CreatedAt));
            }
            else if (request.Status == RequestStatus.WaitingForResidentClosure)
            {
                activeRequirement?.CloseWithoutAnswer(history.CreatedAt);
                dbContext.RequestMessages.Add(new RequestMessage(request.Id,
                    history.ChangedByUserId, history.Reason!, MessageChannel.Portal));
                dbContext.RequestClosureConfirmations.Add(new RequestClosureConfirmation(
                    request.Id, history.Id, history.Reason!, history.CreatedAt));
            }
            else
            {
                activeRequirement?.CloseWithoutAnswer(history.CreatedAt);
                await dbContext.RequestClosureConfirmations
                    .Where(item => item.RequestId == request.Id && item.Status == RequestClosureConfirmationStatus.Pending)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.Status, RequestClosureConfirmationStatus.Cancelled)
                        .SetProperty(item => item.DecidedAt, history.CreatedAt)
                        .SetProperty(item => item.UpdatedAt, history.CreatedAt), cancellationToken);
                var unreadRequirements = await dbContext.RequestResidentReplyRequirements
                    .Where(item => item.RequestId == request.Id && item.HasUnreadAnswer)
                    .ToListAsync(cancellationToken);
                foreach (var requirement in unreadRequirements)
                    requirement.MarkAnswerRead(history.CreatedAt);
            }
            dbContext.RequestStatusHistories.Add(history);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public sealed record RequestDto(string? Status, string? Reason);

    public sealed record Response(
        Guid Id,
        string Status,
        string Priority,
        DateTime UpdatedAt,
        DateTime? ResolvedAt);
}
