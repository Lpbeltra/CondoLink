using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppConversationInactivityWorker(
    IServiceScopeFactory scopes, IOptions<WhatsAppOptions> options,
    OperationalTelemetry telemetry,
    ILogger<WhatsAppConversationInactivityWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan PollingInterval = TimeSpan.FromMinutes(1);
    internal const string Message = "O fluxo de abertura foi encerrado por inatividade. Quando quiser, é só me chamar novamente para abrir uma nova solicitação.";
    internal static readonly WhatsAppConversationState[] DraftStates =
    [
        WhatsAppConversationState.SelectingUnit,
        WhatsAppConversationState.SelectingCategory,
        WhatsAppConversationState.CollectingDescription,
        WhatsAppConversationState.CollectingNewRequestAttachments,
        WhatsAppConversationState.ReviewingNewRequest
    ];

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await telemetry.RecordWorkerAsync(nameof(WhatsAppConversationInactivityWorker),
                    true, PollingInterval, "started", ct: ct);
                var processed = await ProcessExpiredAsync(DateTime.UtcNow, ct);
                await telemetry.RecordWorkerAsync(nameof(WhatsAppConversationInactivityWorker),
                    true, PollingInterval, "completed", true, processed, ct: ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "WhatsApp draft inactivity cycle failed.");
                await telemetry.RecordWorkerAsync(nameof(WhatsAppConversationInactivityWorker),
                    true, PollingInterval, "completed", false, failures: 1,
                    code: "cycle_failed", ct: CancellationToken.None);
            }
            await Task.Delay(PollingInterval, ct);
        }
    }

    internal async Task<int> ProcessExpiredAsync(DateTime now, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<LocalFileStorage>();
        var timeout = TimeSpan.FromMinutes(Math.Clamp(options.Value.DraftInactivityMinutes, 1, 1440));
        var cutoff = now - timeout;
        var sessions = await db.WhatsAppSessions
            .Where(x => DraftStates.Contains(x.State) && x.RequestId == null
                && x.UserId != null && x.CondominiumId != null
                && x.LastInteractionAt <= cutoff)
            .OrderBy(x => x.LastInteractionAt).Take(100).ToArrayAsync(ct);
        var processed = 0;
        foreach (var session in sessions)
        {
            var version = session.Version;
            var userId = session.UserId!.Value;
            var condominiumId = session.CondominiumId!.Value;
            var lastInteractionAt = session.LastInteractionAt;
            var attachments = await db.WhatsAppDraftAttachments
                .Where(x => x.SessionId == session.Id).ToArrayAsync(ct);
            var keys = attachments.Select(x => x.StorageKey).ToArray();
            db.WhatsAppDraftAttachments.RemoveRange(attachments);
            session.End(now);
            db.WhatsAppOutboundMessages.Add(new WhatsAppOutboundMessage(
                null, null, userId, condominiumId,
                session.PhoneNumber, WhatsAppNotificationType.DraftInactivityTimeout,
                WhatsAppSendMode.SessionText, $"draft-timeout:{session.Id}:{version}",
                Message, null, null, now));
            try
            {
                await db.SaveChangesAsync(ct);
                foreach (var key in keys) storage.Delete(key);
                processed++;
                logger.LogInformation("WhatsApp draft {SessionId} ended. CondominiumId: {CondominiumId}; Reason: InactivityTimeout; LastActivityAt: {LastActivityAt}; InactivitySeconds: {InactivitySeconds}; OutboundCreated: true.",
                    session.Id, condominiumId, lastInteractionAt,
                    (long)(now - lastInteractionAt).TotalSeconds);
            }
            catch (DbUpdateConcurrencyException)
            {
                foreach (var entry in db.ChangeTracker.Entries()) entry.State = EntityState.Detached;
                break;
            }
        }
        if (processed > 0)
            await telemetry.EventAsync("WhatsApp", "Draft", "Info", "inactivity_timeout",
                ct: ct);
        return processed;
    }
}
