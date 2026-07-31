using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppOutboundWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppOutboundWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.Value;
            if (settings.Enabled && settings.OutboundWorkerEnabled)
            {
                try { await ProcessBatch(settings, stoppingToken); }
                catch (Exception ex)
                {
                    logger.LogError(ex, "WhatsApp outbound worker batch failed.");
                }
            }
            await Task.Delay(
                TimeSpan.FromSeconds(Math.Clamp(settings.OutboundPollingSeconds, 5, 300)),
                stoppingToken);
        }
    }

    internal async Task ProcessBatch(
        WhatsAppOptions settings,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var client = scope.ServiceProvider.GetRequiredService<IWhatsAppClient>();
        var verificationMessageProtector = scope.ServiceProvider
            .GetRequiredService<IPhoneVerificationMessageProtector>();
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
            try
            {
                content = item.NotificationType is
                    WhatsAppNotificationType.PhoneVerification
                    or WhatsAppNotificationType.LoginCode
                    ? verificationMessageProtector.Unprotect(item.Content)
                    : item.Content;
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

            var result = item.SendMode == WhatsAppSendMode.SessionText
                ? await client.SendTextAsync(item.DestinationPhone, content, ct)
                : await client.SendTemplateAsync(
                    item.DestinationPhone, item.TemplateName!, item.TemplateLanguage!, ct);
            logger.LogInformation(
                "WhatsApp provider response received for outbound {OutboundId}. Succeeded: {Succeeded}; ErrorCode: {ErrorCode}.",
                item.Id,
                result.Succeeded,
                result.ErrorCode);
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.ExternalMessageId))
            {
                item.MarkSent(result.ExternalMessageId, DateTime.UtcNow);
                logger.LogInformation(
                    "WhatsApp outbound {OutboundId} accepted by provider.",
                    item.Id);
            }
            else
            {
                var exponent = Math.Min(item.AttemptCount - 1, 6);
                var delay = TimeSpan.FromSeconds(
                    Math.Clamp(settings.OutboundInitialRetrySeconds, 10, 3600)
                    * Math.Pow(2, exponent));
                item.MarkFailure(
                    result.ErrorCode,
                    result.Error ?? "Provider did not return a message id.",
                    result.IsTransient,
                    Math.Clamp(settings.OutboundMaxAttempts, 1, 10),
                    DateTime.UtcNow,
                    delay);
                logger.Log(
                    result.IsTransient
                        ? LogLevel.Warning : LogLevel.Error,
                    "WhatsApp outbound {OutboundId} failed transient {Transient} code {ErrorCode}.",
                    item.Id, result.IsTransient, result.ErrorCode);
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "WhatsApp outbound {OutboundId} processed with status {Status}.",
                item.Id,
                item.Status);
        }
    }
}
