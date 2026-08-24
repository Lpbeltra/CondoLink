using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.Auth;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppOutboundWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WhatsAppOptions> options,
    OperationalTelemetry telemetry,
    ILogger<WhatsAppOutboundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.Value;
            var interval = TimeSpan.FromSeconds(Math.Clamp(settings.OutboundPollingSeconds, 5, 300));
            await telemetry.RecordWorkerAsync(nameof(WhatsAppOutboundWorker), settings.Enabled && settings.OutboundWorkerEnabled, interval, "heartbeat", ct: stoppingToken);
            if (settings.Enabled && settings.OutboundWorkerEnabled)
            {
                try { await telemetry.RecordWorkerAsync(nameof(WhatsAppOutboundWorker), true, interval, "started", ct: stoppingToken); var count = await ProcessBatch(settings, stoppingToken); await telemetry.RecordWorkerAsync(nameof(WhatsAppOutboundWorker), true, interval, "completed", true, count, ct: stoppingToken); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "WhatsApp outbound worker batch failed.");
                    await telemetry.RecordWorkerAsync(nameof(WhatsAppOutboundWorker), true, interval, "completed", false, failures: 1, code: "batch_failed", ct: CancellationToken.None);
                    await telemetry.EventAsync("WhatsApp", "OutboundWorker", "Error", "batch_failed", ct: CancellationToken.None);
                }
            }
            await Task.Delay(
                interval,
                stoppingToken);
        }
    }

    internal async Task<int> ProcessBatch(
        WhatsAppOptions settings,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<IWhatsAppClient>();
        var verificationMessageProtector = scope.ServiceProvider
            .GetRequiredService<IPhoneVerificationMessageProtector>();
        var firstAccessProtector = scope.ServiceProvider
            .GetRequiredService<IFirstAccessWhatsAppPayloadProtector>();
        var now = DateTime.UtcNow;
        var interrupted = await db.WhatsAppOutboundMessages
            .Where(x => x.Status == WhatsAppOutboundStatus.Processing
                && x.NextAttemptAt < now.AddMinutes(-10))
            .ToListAsync(ct);
        foreach (var message in interrupted)
            message.RecoverInterruptedProcessing(now);
        if (interrupted.Count > 0)
            await db.SaveChangesAsync(ct);
        var items = await db.WhatsAppOutboundMessages
            .Where(x => x.Status == WhatsAppOutboundStatus.Pending
                && x.NextAttemptAt <= now)
            .OrderBy(x => x.NextAttemptAt)
            .Take(Math.Clamp(settings.OutboundBatchSize, 1, 50))
            .ToArrayAsync(ct);
        foreach (var item in items)
        {
            logger.LogInformation(
                "WhatsApp outbound {OutboundId} processing type {NotificationType} attempt {Attempt}.",
                item.Id, item.NotificationType, item.AttemptCount + 1);
            item.StartProcessing();
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                db.Entry(item).State = EntityState.Detached;
                continue;
            }

            string content;
            FirstAccessWhatsAppPayload? firstAccessPayload = null;
            try
            {
                content = item.NotificationType is
                    WhatsAppNotificationType.PhoneVerification
                    or WhatsAppNotificationType.LoginCode
                    ? verificationMessageProtector.Unprotect(item.Content)
                    : item.Content;
                if (item.NotificationType == WhatsAppNotificationType.ResidentFirstAccess)
                    firstAccessPayload = firstAccessProtector.Unprotect(item.Content);
            }
            catch (System.Security.Cryptography.CryptographicException)
            {
                item.MarkFailure(
                    "message_protection",
                    "Protected verification message could not be read.",
                    false,
                    1,
                    DateTime.UtcNow,
                    TimeSpan.Zero);
                await db.SaveChangesAsync(ct);
                continue;
            }

            WhatsAppSendResult result;
            if (item.SendMode == WhatsAppSendMode.SessionText)
                result = await client.SendTextAsync(item.DestinationPhone, content, ct);
            else
            {
                IReadOnlyList<string> parameters = [];
                IReadOnlyList<string> quickReplies = [];
                IReadOnlyList<string> urlButtons = [];
                string? bodyParameterName = null;
                if (item.NotificationType == WhatsAppNotificationType.InformationRequested)
                {
                    var fullName = await db.Set<ApplicationUser>().AsNoTracking()
                        .Where(x => x.Id == item.UserId)
                        .Select(x => x.FullName)
                        .SingleAsync(ct);
                    parameters = [SafeFirstName(fullName)];
                    quickReplies = ["resident_reply_now", "resident_reply_later"];
                    bodyParameterName = settings.Templates.InformationRequested
                        .BodyParameterName;
                }
                else if (item.NotificationType is WhatsAppNotificationType.StatusChanged
                    or WhatsAppNotificationType.RequestResolved
                    or WhatsAppNotificationType.RequestCancelled
                    or WhatsAppNotificationType.RequestReopened)
                {
                    var fullName = await db.Set<ApplicationUser>().AsNoTracking()
                        .Where(x => x.Id == item.UserId)
                        .Select(x => x.FullName)
                        .SingleAsync(ct);
                    if (item.NotificationType == WhatsAppNotificationType.StatusChanged
                        && item.RequestClosureConfirmationId.HasValue)
                    {
                        parameters = ClosureTemplateParameters(fullName,
                            item.TemplateParameterContent ?? content);
                        quickReplies = ["closure_confirm", "closure_question"];
                    }
                    else if (item.NotificationType == WhatsAppNotificationType.RequestResolved)
                    {
                        var requestTitle = await db.Requests.AsNoTracking()
                            .Where(x => x.Id == item.RequestId)
                            .Select(x => x.Title).SingleAsync(ct);
                        parameters = FinalizationTemplateParameters(fullName,
                            requestTitle, item.TemplateParameterContent ?? content);
                    }
                    else
                    {
                        parameters = StatusChangedTemplateParameters(fullName);
                        quickReplies = ["request_status_view"];
                    }
                }
                else if (item.NotificationType == WhatsAppNotificationType.ManagerNewRequest)
                {
                    parameters = ManagerNewRequestTemplateParameters(
                        item.TemplateParameterContent);
                }
                else if (item.NotificationType == WhatsAppNotificationType.ResidentFirstAccess)
                {
                    parameters = [firstAccessPayload!.ResidentName, firstAccessPayload.CondominiumName];
                    urlButtons = [firstAccessPayload.ButtonParameter];
                }
                result = await client.SendTemplateAsync(item.DestinationPhone,
                    item.TemplateName!, item.TemplateLanguage!, parameters,
                    quickReplies, ct, bodyParameterName, urlButtons);
            }
            result = EnsureFailureDiagnostic(result);
            logger.LogInformation(
                "WhatsApp provider response received for outbound {OutboundId}. Success: {Success}; IsTransient: {IsTransient}; ErrorCode: {ErrorCode}; HttpStatus: {HttpStatus}; FailureKind: {FailureKind}; FailureStage: {FailureStage}.",
                item.Id,
                result.Succeeded,
                result.IsTransient,
                result.ErrorCode,
                result.HttpStatusCode,
                result.FailureKind,
                result.FailureStage);
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.ExternalMessageId))
            {
                item.MarkSent(result.ExternalMessageId, DateTime.UtcNow);
                if (item.NotificationType == WhatsAppNotificationType.ResidentFirstAccess)
                {
                    var user = await db.Set<ApplicationUser>().SingleAsync(x => x.Id == item.UserId, ct);
                    user.MarkFirstAccessInviteSent(DateTime.UtcNow);
                }
                logger.LogInformation(
                    "WhatsApp outbound {OutboundId} accepted by provider.",
                    item.Id);
            }
            else
            {
                ApplyFailure(item, result, settings, DateTime.UtcNow);
                if (item.NotificationType == WhatsAppNotificationType.ResidentFirstAccess
                    && item.Status is WhatsAppOutboundStatus.PermanentlyFailed or WhatsAppOutboundStatus.Failed)
                {
                    var user = await db.Set<ApplicationUser>().SingleAsync(x => x.Id == item.UserId, ct);
                    user.MarkFirstAccessInviteFailed(DateTime.UtcNow);
                }
                if (item.Status is WhatsAppOutboundStatus.PermanentlyFailed or WhatsAppOutboundStatus.Failed)
                    await telemetry.EventAsync("WhatsApp", "OutboundExhausted", "Error",
                        OperationalTelemetry.SafeCode(result.ErrorCode), item.Id.ToString(), ct);
                logger.Log(
                    result.IsTransient
                        ? LogLevel.Warning : LogLevel.Error,
                    "WhatsApp outbound {OutboundId} failed. Success: false; IsTransient: {IsTransient}; ErrorCode: {ErrorCode}; HttpStatus: {HttpStatus}; FailureKind: {FailureKind}; FailureStage: {FailureStage}.",
                    item.Id, result.IsTransient, result.ErrorCode,
                    result.HttpStatusCode, result.FailureKind,
                    result.FailureStage);
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "WhatsApp outbound {OutboundId} processed with status {Status}.",
                item.Id,
                item.Status);
        }
        return items.Length;
    }

    internal static IReadOnlyList<string> ManagerNewRequestTemplateParameters(
        string? serializedPayload)
    {
        if (string.IsNullOrWhiteSpace(serializedPayload))
            throw new InvalidOperationException(
                "Manager new request template payload is missing.");
        var payload = ManagerNewRequestTemplatePayload.Deserialize(serializedPayload);
        return
        [
            payload.CondominiumName,
            payload.ResidentName,
            payload.UnitIdentifier,
            payload.BlockIdentifier,
            payload.RequestTitle
        ];
    }

    internal static IReadOnlyList<string> StatusChangedTemplateParameters(
        string fullName) => [SafeFirstName(fullName)];

    internal static IReadOnlyList<string> ClosureTemplateParameters(
        string fullName, string conclusion) => [SafeFirstName(fullName), conclusion];

    internal static IReadOnlyList<string> FinalizationTemplateParameters(
        string fullName, string requestTitle, string conclusion) =>
        [SafeFirstName(fullName), requestTitle, "FINALIZADA", conclusion];

    internal static string SafeFirstName(string? fullName)
    {
        var displayName = string.IsNullOrWhiteSpace(fullName)
            ? "Morador" : fullName.Trim();
        var separator = displayName.IndexOfAny([' ', '\t', '\r', '\n']);
        var firstName = separator > 0 ? displayName[..separator] : displayName;
        return firstName.Length <= 60 ? firstName : firstName[..60];
    }

    internal static void ApplyFailure(
        CondoLink.Domain.Entities.WhatsAppOutboundMessage item,
        WhatsAppSendResult result,
        WhatsAppOptions settings,
        DateTime now)
    {
        result = EnsureFailureDiagnostic(result);
        var exponent = Math.Min(item.AttemptCount - 1, 6);
        var delay = TimeSpan.FromSeconds(
            Math.Clamp(settings.OutboundInitialRetrySeconds, 10, 3600)
            * Math.Pow(2, exponent));
        item.MarkFailure(
            result.ErrorCode,
            result.Error ?? "Provider did not return a message id.",
            result.IsTransient,
            Math.Clamp(settings.OutboundMaxAttempts, 1, 10),
            now,
            delay);
    }

    internal static WhatsAppSendResult EnsureFailureDiagnostic(
        WhatsAppSendResult result)
    {
        if (result.Succeeded) return result;
        return result with
        {
            ErrorCode = string.IsNullOrWhiteSpace(result.ErrorCode)
                ? "undiagnosed_failure" : result.ErrorCode,
            Error = string.IsNullOrWhiteSpace(result.Error)
                ? "Client returned a failure without a technical description."
                : result.Error,
            FailureKind = string.IsNullOrWhiteSpace(result.FailureKind)
                ? "UndiagnosedClientFailure" : result.FailureKind,
            FailureStage = string.IsNullOrWhiteSpace(result.FailureStage)
                ? "worker_received_result" : result.FailureStage
        };
    }
}
