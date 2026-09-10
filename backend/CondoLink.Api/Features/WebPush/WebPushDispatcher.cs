using System.Net;
using System.Text.Json;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Lib.Net.Http.WebPush;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WebPush;

public sealed class WebPushDispatcher(
    AppDbContext db,
    IWebPushClient client,
    IOptions<WebPushOptions> options,
    TimeProvider timeProvider,
    ILogger<WebPushDispatcher> logger)
{
    public async Task DispatchAsync(Guid notificationId, CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured) return;

        var notification = await db.Notifications.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == notificationId, cancellationToken);
        if (notification?.RequestId is null) return;

        var requestAuthorId = await db.Requests.AsNoTracking()
            .Where(x => x.Id == notification.RequestId.Value)
            .Select(x => (Guid?)x.AuthorUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!requestAuthorId.HasValue) return;

        var subscriptions = await db.WebPushSubscriptions
            .Where(x => x.UserId == notification.RecipientUserId && x.IsActive)
            .ToArrayAsync(cancellationToken);
        logger.LogInformation(
            "Web Push dispatch started. NotificationId: {NotificationId}; UserId: {UserId}; SubscriptionCount: {SubscriptionCount}.",
            notification.Id, notification.RecipientUserId, subscriptions.Length);

        var residentTarget = notification.RecipientUserId == requestAuthorId.Value;
        var url = residentTarget
            ? $"/requests/{notification.RequestId}"
            : $"/management/requests/{notification.RequestId}";
        var payload = JsonSerializer.Serialize(new
        {
            title = "Comvy",
            body = SafeBody(notification.Type),
            url,
            type = notification.Type.ToString(),
            entityId = notification.RequestId
        });

        foreach (var subscription in subscriptions)
        {
            try
            {
                await client.SendAsync(subscription.Endpoint, subscription.P256dh,
                    subscription.Auth, payload, cancellationToken);
                subscription.MarkUsed(timeProvider.GetUtcNow().UtcDateTime);
                logger.LogInformation(
                    "Web Push delivered. NotificationId: {NotificationId}; SubscriptionId: {SubscriptionId}.",
                    notification.Id, subscription.Id);
            }
            catch (PushServiceClientException exception) when (
                exception.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                subscription.Deactivate(timeProvider.GetUtcNow().UtcDateTime);
                logger.LogInformation(
                    "Web Push subscription invalidated. NotificationId: {NotificationId}; SubscriptionId: {SubscriptionId}; StatusCode: {StatusCode}.",
                    notification.Id, subscription.Id, (int)exception.StatusCode);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    "Web Push delivery failed transiently. NotificationId: {NotificationId}; SubscriptionId: {SubscriptionId}; ErrorType: {ErrorType}.",
                    notification.Id, subscription.Id, exception.GetType().Name);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    internal static string SafeBody(NotificationType type) => type switch
    {
        NotificationType.RequestCreated => "Um novo atendimento foi aberto.",
        NotificationType.ResidentRequestUpdated => "Um morador atualizou um atendimento.",
        NotificationType.RequestMessageReceived => "Há uma nova mensagem em um atendimento.",
        NotificationType.RequestStatusChanged => "Há uma atualização no seu atendimento.",
        _ => "Há uma nova atualização no Comvy."
    };
}
