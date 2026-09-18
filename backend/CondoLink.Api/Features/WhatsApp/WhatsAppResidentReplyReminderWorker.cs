using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppResidentReplyReminderWorker(
    IServiceScopeFactory scopes,
    ILogger<WhatsAppResidentReplyReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Delay = TimeSpan.FromHours(3);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await ProcessDueAsync(DateTime.UtcNow, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "WhatsApp resident reply reminder cycle failed."); }
            await Task.Delay(Interval, ct);
        }
    }

    internal async Task<int> ProcessDueAsync(DateTime now, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dueIds = await db.RequestResidentReplyRequirements.AsNoTracking()
            .Where(x => x.IsActive && x.AnswerMessageId == null && x.ReminderCount == 0
                && x.LastReminderAt != null && x.LastReminderAt <= now - Delay)
            .OrderBy(x => x.LastReminderAt).ThenBy(x => x.Id).Take(100)
            .Select(x => x.Id).ToArrayAsync(ct);
        var processed = 0;
        foreach (var id in dueIds)
        {
            var claimed = await db.RequestResidentReplyRequirements.Where(x => x.Id == id
                    && x.IsActive && x.AnswerMessageId == null && x.ReminderCount == 0
                    && x.LastReminderAt != null && x.LastReminderAt <= now - Delay)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.ReminderCount, 1)
                    .SetProperty(x => x.LastReminderAt, now)
                    .SetProperty(x => x.UpdatedAt, now), ct);
            if (claimed != 1) continue;

            var item = await (from requirement in db.RequestResidentReplyRequirements.AsNoTracking()
                              join request in db.Requests.AsNoTracking() on requirement.RequestId equals request.Id
                              join user in db.Set<ApplicationUser>().AsNoTracking() on request.AuthorUserId equals user.Id
                              where requirement.Id == id && requirement.IsActive
                                  && request.Status == RequestStatus.WaitingForResident
                              select new { request.Id, request.CondominiumId, user.FullName, requirement.Question })
                .SingleOrDefaultAsync(ct);
            if (item is null) continue;
            var firstName = item.FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Olá";
            var content = $"Olá, {firstName}! Precisamos de uma informação sua para continuar o atendimento.\n\n{item.Question}\n\nResponda por aqui para continuar.";
            await scope.ServiceProvider.GetRequiredService<WhatsAppNotificationDispatcher>().EnqueueAsync(
                item.Id, WhatsAppNotificationType.InformationRequested,
                $"resident-reply-reminder:{id}:1", content, null, ct);
            processed++;
        }
        return processed;
    }
}
