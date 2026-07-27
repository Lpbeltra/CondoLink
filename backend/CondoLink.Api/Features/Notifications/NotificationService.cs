using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Api.Features.Notifications;

/// <summary>
/// Creates in-app notifications when something happens to a request.
///
/// Fan-out rules live here rather than in the endpoints so the "who should be
/// told" decision is in one place and directly testable.
/// </summary>
public sealed class NotificationService(AppDbContext dbContext)
{
    /// <summary>
    /// Notifies the managers of a condominium that a new request was opened.
    /// The author is never notified about their own action.
    /// </summary>
    public async Task NotifyRequestCreatedAsync(
        DomainRequest request,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var managerIds = await ManagerIdsAsync(
            request.CondominiumId, request.AuthorUserId, cancellationToken);

        AddRange(managerIds.Select(managerId => new Notification(
            managerId,
            request.CondominiumId,
            NotificationType.RequestCreated,
            "Nova solicitação",
            $"{categoryName}: {Shorten(request.Title)}",
            request.Id)));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Notifies the author when a manager changes the status of their request.
    /// If the author made the change themselves, nobody is notified.
    /// </summary>
    public async Task NotifyStatusChangedAsync(
        DomainRequest request,
        RequestStatus previousStatus,
        Guid changedByUserId,
        CancellationToken cancellationToken)
    {
        if (changedByUserId == request.AuthorUserId) return;

        dbContext.Notifications.Add(new Notification(
            request.AuthorUserId,
            request.CondominiumId,
            NotificationType.RequestStatusChanged,
            "Status atualizado",
            $"{Shorten(request.Title)}: {Describe(previousStatus)} → {Describe(request.Status)}",
            request.Id));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Notifies the counterpart of a new message: the author hears about manager
    /// replies, and managers hear about the author's replies.
    /// </summary>
    public async Task NotifyMessageAsync(
        Guid requestId,
        Guid condominiumId,
        Guid requestAuthorUserId,
        string requestTitle,
        Guid messageAuthorUserId,
        string content,
        CancellationToken cancellationToken)
    {
        Guid[] recipients = messageAuthorUserId == requestAuthorUserId
            ? await ManagerIdsAsync(condominiumId, messageAuthorUserId, cancellationToken)
            : [requestAuthorUserId];

        AddRange(recipients
            .Where(recipientId => recipientId != messageAuthorUserId)
            .Select(recipientId => new Notification(
                recipientId,
                condominiumId,
                NotificationType.RequestMessageReceived,
                "Nova mensagem",
                $"{Shorten(requestTitle, 60)}: {Shorten(content, 90)}",
                requestId)));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Active managers of a condominium, excluding one user.</summary>
    private Task<Guid[]> ManagerIdsAsync(
        Guid condominiumId,
        Guid excludeUserId,
        CancellationToken cancellationToken)
        => dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.CondominiumId == condominiumId
                && membership.IsActive
                && membership.EndedAt == null
                && membership.UserId != excludeUserId)
            .Join(
                dbContext.CondominiumMembershipRoles
                    .AsNoTracking()
                    .Where(role =>
                        role.Role == CondominiumRole.Manager
                        && role.IsActive
                        && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (membership, _) => membership.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    private void AddRange(IEnumerable<Notification> notifications)
    {
        foreach (var notification in notifications)
        {
            dbContext.Notifications.Add(notification);
        }
    }

    /// <summary>Keeps bodies within the column limit without throwing.</summary>
    internal static string Shorten(string value, int maximumLength = 160)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, maximumLength - 1).TrimEnd(), "…");
    }

    internal static string Describe(RequestStatus status) => status switch
    {
        RequestStatus.Open => "Aberta",
        RequestStatus.InProgress => "Em andamento",
        RequestStatus.WaitingForResident => "Aguardando morador",
        RequestStatus.WaitingForThirdParty => "Aguardando terceiro",
        RequestStatus.Resolved => "Resolvida",
        RequestStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };
}
