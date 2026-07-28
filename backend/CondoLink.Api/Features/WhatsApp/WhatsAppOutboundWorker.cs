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
            item.StartProcessing();
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException)
            {
                db.Entry(item).State = EntityState.Detached;
                continue;
            }

            var result = item.SendMode == WhatsAppSendMode.SessionText
                ? await client.SendTextAsync(item.DestinationPhone, item.Content, ct)
                : await client.SendTemplateAsync(
                    item.DestinationPhone, item.TemplateName!, item.TemplateLanguage!, ct);
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.ExternalMessageId))
                item.MarkSent(result.ExternalMessageId, DateTime.UtcNow);
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
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
