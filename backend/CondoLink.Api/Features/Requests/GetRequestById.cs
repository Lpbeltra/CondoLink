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
        endpoints.MapPost("/requests/{id:guid}/resident-update-acknowledgement",
                AcknowledgeResidentUpdateAsync)
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
                    AuthorEmail = item.author.Email,
                    AuthorPhoneNumber = item.author.PhoneNumber,
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
                            (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager)
                            && role.IsActive
                            && role.RevokedAt == null),
                    membership => membership.Id,
                    role => role.CondominiumMembershipId,
                    (_, _) => true)
                .AnyAsync(cancellationToken);

        if (request.AuthorUserId != authenticatedUserId && !isCondominiumManager)
        {
            return Results.Json(
                new { error = "You do not have access to this request." },
                statusCode: StatusCodes.Status403Forbidden);
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
                    dbContext.CondominiumBlocks.Where(block => block.Id == unit.BlockId).Select(block => block.Identifier).FirstOrDefault()))
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

        var answeredRequirements = await dbContext.RequestResidentReplyRequirements
            .AsNoTracking()
            .Where(requirement => requirement.RequestId == id
                && requirement.AnswerMessageId != null
                && requirement.AnsweredAt != null)
            .Select(requirement => new
            {
                requirement.AnswerMessageId,
                requirement.AnsweredAt
            })
            .ToListAsync(cancellationToken);

        var statusHistory = historyRows
            .Select(history =>
            {
                var answerMessageId = history.PreviousStatus == RequestStatus.WaitingForResident
                    && history.NewStatus == RequestStatus.InProgress
                    ? answeredRequirements.SingleOrDefault(requirement =>
                        requirement.AnsweredAt == history.CreatedAt)?.AnswerMessageId
                    : null;
                return new StatusHistoryResponse(
                    history.Id,
                    history.PreviousStatus?.ToString(),
                    history.NewStatus.ToString(),
                    history.ChangedByUserId,
                    history.ChangedByFullName,
                    history.Reason,
                    history.CreatedAt,
                    answerMessageId);
            })
            .ToArray();

        RequestAiAnalysisResponse? aiAnalysis = null;
        OriginalReportResponse? originalReport = null;
        ResidentReplyRequirementResponse? residentReplyRequirement = null;
        ResidentClosureProposalResponse? residentClosureProposal = null;
        var hasUnreadResidentReply = false;
        var hasUnreadResidentUpdate = false;
        ResidentSummaryResponse? residentSummary = null;
        if (request.AuthorUserId == authenticatedUserId)
        {
            residentReplyRequirement = await dbContext.RequestResidentReplyRequirements
                .AsNoTracking().Where(x => x.RequestId == id && x.IsActive)
                .Select(x => new ResidentReplyRequirementResponse(x.Id, x.Question,
                    x.RequestedAt, x.IsActive)).SingleOrDefaultAsync(cancellationToken);
            if (request.Status == RequestStatus.WaitingForResidentClosure)
            {
                residentClosureProposal = await dbContext.RequestClosureConfirmations
                    .AsNoTracking()
                    .Where(x => x.RequestId == id
                        && x.Status == RequestClosureConfirmationStatus.Pending)
                    .OrderByDescending(x => x.RequestedAt)
                    .Select(x => new ResidentClosureProposalResponse(
                        x.Conclusion, x.RequestedAt, x.ExpiresAt))
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }
        if (isCondominiumManager)
        {
            var relationship = request.TargetUnitId.HasValue
                ? await dbContext.UnitMemberships.AsNoTracking()
                    .Where(x => x.UserId == request.AuthorUserId
                        && x.UnitId == request.TargetUnitId.Value
                        && x.IsActive && x.EndedAt == null)
                    .Select(x => (UnitRelationshipType?)x.RelationshipType)
                    .SingleOrDefaultAsync(cancellationToken)
                : null;
            residentSummary = new ResidentSummaryResponse(
                request.AuthorFullName, targetUnit?.Block,
                targetUnit?.Identifier, request.AuthorPhoneNumber,
                request.AuthorEmail, relationship?.ToString());
            hasUnreadResidentUpdate = await dbContext.Notifications
                .AsNoTracking().AnyAsync(notification => notification.RequestId == id
                    && notification.RecipientUserId == authenticatedUserId
                    && notification.Type == NotificationType.ResidentRequestUpdated
                    && notification.ReadAt == null, cancellationToken);
            hasUnreadResidentReply = await dbContext.RequestResidentReplyRequirements
                .AsNoTracking().AnyAsync(x => x.RequestId == id && x.HasUnreadAnswer,
                    cancellationToken);
            var analysis = await dbContext.RequestAiAnalyses.AsNoTracking()
                .SingleOrDefaultAsync(x => x.RequestId == id, cancellationToken);
            aiAnalysis = analysis is null
                ? null
                : RequestAiAnalysisResponse.FromEntity(analysis);

            var originalMessage = await dbContext.RequestMessages.AsNoTracking()
                .Where(message => message.RequestId == id
                    && message.AuthorUserId == request.AuthorUserId
                    && message.Channel == MessageChannel.WhatsApp)
                .OrderBy(message => message.CreatedAt)
                .ThenBy(message => message.Id)
                .Select(message => new
                {
                    message.Id,
                    message.Content,
                    message.CreatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);
            if (originalMessage is not null)
            {
                var audio = await dbContext.RequestAttachments.AsNoTracking()
                    .Where(attachment => attachment.RequestId == id
                        && attachment.RequestMessageId == originalMessage.Id
                        && attachment.ContentType.StartsWith("audio/"))
                    .OrderBy(attachment => attachment.CreatedAt)
                    .ThenBy(attachment => attachment.Id)
                    .Select(attachment => new OriginalAudioResponse(
                        attachment.Id,
                        attachment.OriginalFileName,
                        attachment.ContentType,
                        attachment.FileSize,
                        $"/request-attachments/{attachment.Id}/content"))
                    .FirstOrDefaultAsync(cancellationToken);
                originalReport = new OriginalReportResponse(
                    originalMessage.Content, "WhatsApp", originalMessage.CreatedAt, audio);
            }
        }

        AgendaReminderSummaryResponse? agendaReminder = null;
        if (isCondominiumManager)
            agendaReminder = await (from link in dbContext.AgendaReminderRequests.AsNoTracking()
                join reminder in dbContext.AgendaReminders.AsNoTracking()
                    on link.ReminderId equals reminder.Id
                where link.RequestId == request.Id
                select new AgendaReminderSummaryResponse(reminder.Id, reminder.Title,
                    reminder.NextOccurrenceAtUtc, reminder.RecurrenceType.ToString(),
                    reminder.IsActive, reminder.CompletedAt))
                .SingleOrDefaultAsync(cancellationToken);

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
            statusHistory,
            aiAnalysis,
            originalReport,
            residentReplyRequirement,
            residentClosureProposal,
            residentSummary,
            agendaReminder,
            hasUnreadResidentReply,
            hasUnreadResidentUpdate);

        return Results.Ok(response);
    }

    private static async Task<IResult> AcknowledgeResidentUpdateAsync(
        Guid id, ClaimsPrincipal principal, AppDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(value, out var userId)) return Results.Unauthorized();

        var request = await dbContext.Requests.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.CondominiumId })
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null) return Results.NotFound(new { error = "Request not found." });

        var isManager = await dbContext.CondominiumMemberships.AsNoTracking()
            .Where(membership => membership.UserId == userId
                && membership.CondominiumId == request.CondominiumId
                && membership.IsActive && membership.EndedAt == null)
            .Join(dbContext.CondominiumMembershipRoles.AsNoTracking().Where(role =>
                    (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager) && role.IsActive
                    && role.RevokedAt == null),
                membership => membership.Id, role => role.CondominiumMembershipId,
                (_, _) => true)
            .AnyAsync(cancellationToken);
        if (!isManager) return Results.Forbid();

        var notifications = await dbContext.Notifications
            .Where(notification => notification.RequestId == id
                && notification.RecipientUserId == userId
                && notification.Type == NotificationType.ResidentRequestUpdated
                && notification.ReadAt == null)
            .ToListAsync(cancellationToken);
        var acknowledgedAt = DateTime.UtcNow;
        foreach (var notification in notifications)
            notification.MarkAsRead(acknowledgedAt);
        if (notifications.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    public sealed record AuthorResponse(Guid Id, string FullName);
    public sealed record TargetUnitResponse(Guid Id, string Identifier, string? Block);
    public sealed record CategoryResponse(Guid Id, string Name);
    public sealed record OriginalReportResponse(
        string? Text, string Channel, DateTime CreatedAt,
        OriginalAudioResponse? AudioAttachment);
    public sealed record OriginalAudioResponse(Guid Id, string OriginalFileName,
        string ContentType, long FileSize, string ContentUrl);
    public sealed record ResidentReplyRequirementResponse(Guid Id, string Question,
        DateTime RequestedAt, bool IsActive);
    public sealed record ResidentClosureProposalResponse(string Conclusion,
        DateTime RequestedAt, DateTime ExpiresAt);
    public sealed record ResidentSummaryResponse(string FullName, string? Block,
        string? Unit, string? PhoneNumber, string? Email, string? Relationship);
    public sealed record AgendaReminderSummaryResponse(Guid Id, string Title,
        DateTime? NextOccurrenceAtUtc, string RecurrenceType, bool IsActive,
        DateTime? CompletedAt);

    public sealed record StatusHistoryResponse(
        Guid Id,
        string? PreviousStatus,
        string NewStatus,
        Guid ChangedByUserId,
        string ChangedByFullName,
        string? Reason,
        DateTime CreatedAt,
        Guid? AnswerMessageId);

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
        IReadOnlyList<StatusHistoryResponse> StatusHistory,
        RequestAiAnalysisResponse? AiAnalysis,
        OriginalReportResponse? OriginalReport,
        ResidentReplyRequirementResponse? ResidentReplyRequirement,
        ResidentClosureProposalResponse? ResidentClosureProposal,
        ResidentSummaryResponse? ResidentSummary,
        AgendaReminderSummaryResponse? AgendaReminder,
        bool HasUnreadResidentReply,
        bool HasUnreadResidentUpdate)
    {
        public string Protocol => RequestProtocol.From(Id);
    }
}
