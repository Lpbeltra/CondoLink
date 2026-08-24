using System.Net;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Observability;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.Agenda;

public sealed class AgendaReminderWorker(IServiceScopeFactory scopes,
    IOptions<AgendaOptions> options, OperationalTelemetry telemetry,
    ILogger<AgendaReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var interval = TimeSpan.FromSeconds(Math.Clamp(
            options.Value.WorkerIntervalSeconds, 30, 300));
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var count = await ProcessBatchAsync(DateTime.UtcNow, ct);
                await telemetry.RecordWorkerAsync(nameof(AgendaReminderWorker), true,
                    interval, "completed", true, count, ct: ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Agenda reminder worker batch failed.");
                await telemetry.EventAsync("Agenda", "Worker", "Error",
                    "batch_failed", ct: CancellationToken.None);
            }
            await Task.Delay(interval, ct);
        }
    }

    internal async Task<int> ProcessBatchAsync(DateTime now, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var due = await db.AgendaReminders.AsNoTracking()
            .Where(x => x.IsActive && x.NextOccurrenceAtUtc != null
                && x.NextOccurrenceAtUtc <= now)
            .OrderBy(x => x.NextOccurrenceAtUtc).ThenBy(x => x.Id)
            .Take(Math.Clamp(options.Value.WorkerBatchSize, 1, 100))
            .Select(x => new { x.Id, Scheduled = x.NextOccurrenceAtUtc!.Value })
            .ToArrayAsync(ct);
        var processed = 0;
        foreach (var item in due)
        {
            try
            {
                var occurrenceId = await ClaimAsync(db, item.Id, item.Scheduled, now, ct);
                if (!occurrenceId.HasValue) continue;
                await DeliverAsync(db, scope.ServiceProvider, occurrenceId.Value, now, ct);
                processed++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Agenda occurrence failed for ReminderId {ReminderId}.", item.Id);
                db.ChangeTracker.Clear();
            }
        }
        return processed;
    }

    private static async Task<Guid?> ClaimAsync(AppDbContext db, Guid reminderId,
        DateTime scheduled, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var reminder = await db.AgendaReminders.SingleOrDefaultAsync(x => x.Id == reminderId
            && x.IsActive && x.NextOccurrenceAtUtc == scheduled, ct);
        if (reminder is null) { await tx.RollbackAsync(ct); return null; }
        var occurrence = new AgendaReminderOccurrence(reminder.Id, scheduled,
            reminder.NotifyByEmail, reminder.NotifyByWhatsApp, now);
        db.Add(occurrence);
        reminder.Advance(scheduled, AgendaRecurrence.Next(scheduled,
            reminder.RecurrenceType, reminder.RecurrenceDayOfMonth,
            reminder.TimeZoneId), now);
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return occurrence.Id; }
        catch (DbUpdateException) { await tx.RollbackAsync(ct); db.ChangeTracker.Clear(); return null; }
    }

    private async Task DeliverAsync(AppDbContext db, IServiceProvider services,
        Guid occurrenceId, DateTime now, CancellationToken ct)
    {
        var data = await (from occurrence in db.AgendaReminderOccurrences
            join reminder in db.AgendaReminders on occurrence.ReminderId equals reminder.Id
            join condominium in db.Condominiums on reminder.CondominiumId equals condominium.Id
            where occurrence.Id == occurrenceId
            select new { Occurrence = occurrence, Reminder = reminder,
                CondominiumName = condominium.Name }).SingleAsync(ct);
        var managers = await (from membership in db.CondominiumMemberships.AsNoTracking()
            join role in db.CondominiumMembershipRoles.AsNoTracking()
                on membership.Id equals role.CondominiumMembershipId
            join user in db.Set<ApplicationUser>().AsNoTracking()
                on membership.UserId equals user.Id
            where membership.CondominiumId == data.Reminder.CondominiumId
                && membership.IsActive && membership.EndedAt == null
                && role.Role == CondominiumRole.Manager && role.IsActive
                && role.RevokedAt == null && user.IsActive
            select new { User = user }).ToArrayAsync(ct);
        if (managers.Length != 1)
        {
            var code = managers.Length == 0 ? "manager_not_found" : "manager_ambiguous";
            if (data.Occurrence.EmailStatus == AgendaDeliveryStatus.Pending)
                data.Occurrence.EmailResult(false, code, now);
            if (data.Occurrence.WhatsAppStatus == AgendaDeliveryStatus.Pending)
                data.Occurrence.WhatsAppResult(AgendaDeliveryStatus.Skipped, code, null, now);
            await db.SaveChangesAsync(ct); return;
        }
        var manager = managers[0].User;
        var local = TimeZoneInfo.ConvertTimeFromUtc(data.Occurrence.ScheduledForUtc,
            TimeZoneInfo.FindSystemTimeZoneById(data.Reminder.TimeZoneId));
        var unit = data.Reminder.UnitId.HasValue
            ? await (from u in db.Units.AsNoTracking()
                join b in db.CondominiumBlocks.AsNoTracking() on u.BlockId equals b.Id into blocks
                from b in blocks.DefaultIfEmpty() where u.Id == data.Reminder.UnitId
                select (b == null ? "" : (b.Identifier.StartsWith("Bloco ") ? b.Identifier : "Bloco " + b.Identifier) + " · ")
                    + "Apto " + u.Identifier).SingleOrDefaultAsync(ct) : null;
        var requestCount = await db.AgendaReminderRequests.CountAsync(x =>
            x.ReminderId == data.Reminder.Id, ct);
        var text = BuildText(data.Reminder, data.CondominiumName, local, unit, requestCount);
        if (data.Occurrence.EmailStatus == AgendaDeliveryStatus.Pending)
        {
            if (!manager.EmailDeliveryEnabled || string.IsNullOrWhiteSpace(manager.Email))
                data.Occurrence.EmailResult(false, "manager_email_unavailable", now);
            else try
            {
                await services.GetRequiredService<IEmailSender>().SendAsync(manager.Email,
                    $"Comvy · Lembrete: {data.Reminder.Title}",
                    $"<p>{WebUtility.HtmlEncode(text).Replace("\n", "<br>")}</p>", ct);
                data.Occurrence.EmailResult(true, null, now);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Agenda email delivery failed for occurrence {OccurrenceId}.", occurrenceId);
                data.Occurrence.EmailResult(false, "email_send_failed", now);
            }
        }
        if (data.Occurrence.WhatsAppStatus == AgendaDeliveryStatus.Pending)
        {
            if (string.IsNullOrWhiteSpace(manager.NormalizedPhoneNumber))
                data.Occurrence.WhatsAppResult(AgendaDeliveryStatus.Skipped,
                    "manager_phone_unavailable", null, now);
            else
            {
                var cutoff = now.AddHours(-24);
                var sessionOpen = await db.WhatsAppInboundMessages.AsNoTracking()
                    .AnyAsync(x => x.ReceivedAt >= cutoff
                        && (x.IdentifiedUserId == manager.Id
                            || x.PhoneNumber == manager.NormalizedPhoneNumber), ct);
                var wa = services.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
                if (!wa.Enabled)
                    data.Occurrence.WhatsAppResult(AgendaDeliveryStatus.Skipped,
                        "whatsapp_disabled", null, now);
                else if (!sessionOpen)
                    data.Occurrence.WhatsAppResult(AgendaDeliveryStatus.Skipped,
                        string.IsNullOrWhiteSpace(wa.Templates.ManagerAgendaReminder.Name)
                            ? "template_not_configured" : "template_contract_pending",
                        null, now);
                else
                {
                    var outbound = new WhatsAppOutboundMessage(null, null, manager.Id,
                        data.Reminder.CondominiumId, manager.NormalizedPhoneNumber,
                        WhatsAppNotificationType.ManagerAgendaReminder,
                        WhatsAppSendMode.SessionText, $"agenda:{occurrenceId}:whatsapp",
                        text, null, null, now);
                    db.Add(outbound);
                    data.Occurrence.WhatsAppResult(AgendaDeliveryStatus.Queued,
                        "outbound_enqueued", outbound.Id, now);
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private static string BuildText(AgendaReminder reminder, string condominium,
        DateTime local, string? unit, int requestCount) =>
        $"*Lembrete da Agenda*\n\n{reminder.Title}\n{reminder.Description}\n\n"
        + $"Condomínio: {condominium}\nData: {local:dd/MM/yyyy HH:mm}"
        + (unit is null ? "" : $"\nUnidade: {unit}")
        + (reminder.RelatedThirdParty is null ? "" : $"\nTerceiro: {reminder.RelatedThirdParty}")
        + (requestCount == 0 ? "" : $"\nAtendimentos relacionados: {requestCount}");
}
