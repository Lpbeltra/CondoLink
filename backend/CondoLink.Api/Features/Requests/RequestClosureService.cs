using CondoLink.Api.Features.Notifications;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CondoLink.Api.Features.Agenda;

namespace CondoLink.Api.Features.Requests;

public sealed class RequestClosureService(AppDbContext db, NotificationService notifications,
    RequestAiAnalysisRefresher? analysis = null,
    ILogger<RequestClosureService>? logger = null)
{
    public Task<Result> ConfirmAsync(Guid requestId, Guid residentId, CancellationToken ct) =>
        DecideAsync(requestId, null, residentId, null, true, MessageChannel.Portal, ct);
    public Task<Result> ConfirmAsync(Guid requestId, Guid confirmationId,
        Guid residentId, CancellationToken ct) =>
        DecideAsync(requestId, confirmationId, residentId, null, true,
            MessageChannel.WhatsAppResidentUpdate, ct);
    public Task<Result> QuestionAsync(Guid requestId, Guid residentId, string text, CancellationToken ct) =>
        QuestionAsync(requestId, residentId, text, MessageChannel.WhatsAppResidentUpdate, ct);
    public Task<Result> QuestionAsync(Guid requestId, Guid residentId, string text,
        MessageChannel channel, CancellationToken ct) =>
        DecideAsync(requestId, null, residentId, text.Trim(), false, channel, ct);

    public Task<Result> QuestionAsync(Guid requestId, Guid confirmationId,
        Guid residentId, string text, CancellationToken ct) =>
        DecideAsync(requestId, confirmationId, residentId,
            text.Trim(), false,
            MessageChannel.WhatsAppResidentUpdate, ct);

    private async Task<Result> DecideAsync(Guid requestId, Guid? confirmationId,
        Guid residentId, string? question, bool confirmed,
        MessageChannel channel, CancellationToken ct)
    {
        var request = await db.Requests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == requestId, ct);
        if (request is null || request.AuthorUserId != residentId) return new(false, "not_found");
        if (!confirmed && string.IsNullOrWhiteSpace(question)) return new(false, "question_required");
        if (!confirmed && question!.Length > RequestMessage.MaximumContentLength) return new(false, "question_too_long");
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        RequestMessage? message = null;
        if (!confirmed) { message = new RequestMessage(requestId, residentId, question!, channel); db.RequestMessages.Add(message); await db.SaveChangesAsync(ct); }
        var target = confirmed ? RequestStatus.Resolved : RequestStatus.InProgress;
        var changed = await db.Requests.Where(x => x.Id == requestId && x.Status == RequestStatus.WaitingForResidentClosure)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, target).SetProperty(x => x.UpdatedAt, now)
                .SetProperty(x => x.ResolvedAt, confirmed ? (DateTime?)now : null), ct);
        var pending = await db.RequestClosureConfirmations.Where(x => x.RequestId == requestId
                && (!confirmationId.HasValue || x.Id == confirmationId.Value)
                && x.Status == RequestClosureConfirmationStatus.Pending && x.ExpiresAt > now)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, confirmed ? RequestClosureConfirmationStatus.Confirmed : RequestClosureConfirmationStatus.Questioned)
                .SetProperty(x => x.DecidedAt, now).SetProperty(x => x.ResponseMessageId, message == null ? null : (Guid?)message.Id)
                .SetProperty(x => x.UpdatedAt, now), ct);
        if (changed != 1 || pending != 1) { await tx.RollbackAsync(ct); return new(false, "already_decided"); }
        await RequestAgendaLinkService.UnlinkIfTerminalAsync(db, requestId, target, ct);
        var historyText = confirmed ? "Morador confirmou a conclusão do atendimento."
            : $"Novo questionamento do morador: {question![..Math.Min(question.Length, 462)]}";
        db.RequestStatusHistories.Add(new RequestStatusHistory(requestId, RequestStatus.WaitingForResidentClosure,
            target, residentId, historyText, now));
        if (message is not null) await notifications.NotifyMessageAsync(requestId, request.CondominiumId,
            residentId, request.Title, residentId, question!, ct, message.Id, channel);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        if (analysis is not null) await analysis.RefreshAsync(requestId, confirmed ? "closure_confirmed" : "closure_questioned", ct);
        return new(true, confirmed ? "confirmed" : "questioned");
    }

    public async Task<int> ExpireBatchAsync(DateTime now, int size, CancellationToken ct)
    {
        var rows = await db.RequestClosureConfirmations.AsNoTracking()
            .Where(x => x.Status == RequestClosureConfirmationStatus.Pending && x.ExpiresAt <= now)
            .Join(db.RequestStatusHistories.AsNoTracking(), x => x.RequestStatusHistoryId, x => x.Id,
                (confirmation, history) => new { confirmation.RequestId, confirmation.ExpiresAt, history.ChangedByUserId })
            .OrderBy(x => x.ExpiresAt).ThenBy(x => x.RequestId).Take(size).ToArrayAsync(ct);
        var count = 0;
        foreach (var row in rows)
        {
            try
            {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var changed = await db.Requests.Where(x => x.Id == row.RequestId && x.Status == RequestStatus.WaitingForResidentClosure)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, RequestStatus.Resolved).SetProperty(x => x.UpdatedAt, now).SetProperty(x => x.ResolvedAt, now), ct);
            var pending = await db.RequestClosureConfirmations.Where(x => x.RequestId == row.RequestId
                    && x.Status == RequestClosureConfirmationStatus.Pending && x.ExpiresAt <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, RequestClosureConfirmationStatus.Expired)
                    .SetProperty(x => x.DecidedAt, now).SetProperty(x => x.FinalizedAutomatically, true).SetProperty(x => x.UpdatedAt, now), ct);
            if (changed == 1 && pending == 1)
            {
                await RequestAgendaLinkService.UnlinkIfTerminalAsync(db,
                    row.RequestId, RequestStatus.Resolved, ct);
                var sessions = await db.WhatsAppSessions
                    .Where(x => x.RequestId == row.RequestId).ToArrayAsync(ct);
                foreach (var session in sessions) session.End(now);
                db.RequestStatusHistories.Add(new RequestStatusHistory(row.RequestId, RequestStatus.WaitingForResidentClosure,
                    RequestStatus.Resolved, row.ChangedByUserId,
                    "O prazo para manifestação do morador foi encerrado sem novo questionamento.", now));
                await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); count++;
                if (analysis is not null) await analysis.RefreshAsync(row.RequestId, "closure_expired", ct);
            }
            else await tx.RollbackAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger?.LogError(exception,
                    "Request closure expiration failed for RequestId {RequestId}.",
                    row.RequestId);
                db.ChangeTracker.Clear();
            }
        }
        return count;
    }
    public sealed record Result(bool Succeeded, string Code);
}
