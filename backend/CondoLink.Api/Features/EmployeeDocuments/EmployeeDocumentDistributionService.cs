using CondoLink.Api.Features.CondominiumModules;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.EmployeeDocuments;

public sealed class EmployeeDocumentDistributionService(
    AppDbContext db, IOptions<WhatsAppOptions> whatsAppOptions, ICondominiumModuleService modules)
{
    /// <summary>
    /// Idempotent: flips a batch from Distributing to Completed once every
    /// delivery belonging to it has reached a terminal outbound status. Safe to
    /// call repeatedly and from multiple call sites (webhook, worker, right
    /// after queueing) — a no-op whenever the batch isn't currently
    /// Distributing, or any delivery is still Pending/Processing.
    /// </summary>
    public async Task<bool> TryCompleteBatchAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await db.EmployeeDocumentBatches.SingleOrDefaultAsync(x => x.Id == batchId, ct);
        if (batch is null || batch.Status != EmployeeDocumentBatchStatus.Distributing) return false;

        // The automatic worker only ever revisits Pending (queued for a first
        // send or an automatic retry) or Processing (mid-send) messages. Every
        // other status — success (Sent/Delivered/Read) or one the automatic
        // loop will never touch again on its own (Failed via webhook,
        // PermanentlyFailed, Cancelled, Skipped) — means "no further automatic
        // action pending" for that message, which is what "distribution
        // finished" means here. It does NOT mean every message succeeded.
        var hasNonTerminalDelivery = await (
            from delivery in db.EmployeeDocumentDeliveries.AsNoTracking()
            join outbound in db.WhatsAppOutboundMessages.AsNoTracking() on delivery.OutboundMessageId equals outbound.Id
            where delivery.BatchId == batchId
            select outbound.Status).AnyAsync(status => status == WhatsAppOutboundStatus.Pending || status == WhatsAppOutboundStatus.Processing, ct);
        if (hasNonTerminalDelivery) return false;

        batch.MarkCompleted();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<EmployeeDocumentDistributionSummaryResponse> GetSummaryAsync(
        Guid condominiumId, Guid batchId, CancellationToken ct)
    {
        var confirmed = await db.EmployeeDocuments.AsNoTracking()
            .Where(x => x.BatchId == batchId && x.CondominiumId == condominiumId
                && x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed)
            .Select(x => new { x.Id, x.EmployeeId })
            .ToArrayAsync(ct);

        var employeeIds = confirmed.Select(x => x.EmployeeId!.Value).Distinct().ToArray();
        var employees = await db.Employees.AsNoTracking()
            .Where(x => employeeIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => new { x.IsActive, x.NormalizedPhoneNumber }, ct);

        var alreadyQueued = (await db.EmployeeDocumentDeliveries.AsNoTracking()
            .Where(x => x.BatchId == batchId).Select(x => x.EmployeeDocumentId).ToArrayAsync(ct)).ToHashSet();

        int ready = 0, noPhone = 0, invalidPhone = 0, alreadyQueuedOrSent = 0;
        foreach (var document in confirmed)
        {
            if (alreadyQueued.Contains(document.Id)) { alreadyQueuedOrSent++; continue; }
            if (!employees.TryGetValue(document.EmployeeId!.Value, out var employee) || !employee.IsActive) { noPhone++; continue; }
            if (string.IsNullOrWhiteSpace(employee.NormalizedPhoneNumber)) { noPhone++; continue; }
            if (!employee.NormalizedPhoneNumber.StartsWith('+')) { invalidPhone++; continue; }
            ready++;
        }

        return new EmployeeDocumentDistributionSummaryResponse(batchId, confirmed.Length, ready, noPhone, invalidPhone, alreadyQueuedOrSent);
    }

    public async Task<int> DistributeAsync(Guid condominiumId, Guid batchId, Guid actorUserId, CancellationToken ct)
    {
        var batch = await db.EmployeeDocumentBatches
            .SingleOrDefaultAsync(x => x.Id == batchId && x.CondominiumId == condominiumId, ct)
            ?? throw new InvalidOperationException("Batch not found.");
        if (batch.Status is not (EmployeeDocumentBatchStatus.Confirmed or EmployeeDocumentBatchStatus.Distributing
            or EmployeeDocumentBatchStatus.Completed))
            throw new InvalidOperationException("Batch must be confirmed before distribution.");
        if (!await modules.IsEnabledAsync(condominiumId, CondominiumModuleType.EmployeeManagement, ct))
            throw new InvalidOperationException("Employee Management module is no longer enabled for this condominium.");

        var template = whatsAppOptions.Value.Templates.EmployeePayslipAvailable;
        if (string.IsNullOrWhiteSpace(template.Name) || string.IsNullOrWhiteSpace(template.Language))
            throw new InvalidOperationException("Payslip WhatsApp template is not configured.");

        var alreadyQueued = (await db.EmployeeDocumentDeliveries.AsNoTracking()
            .Where(x => x.BatchId == batchId).Select(x => x.EmployeeDocumentId).ToArrayAsync(ct)).ToHashSet();

        var confirmed = await db.EmployeeDocuments
            .Where(x => x.BatchId == batchId && x.CondominiumId == condominiumId
                && x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed
                && !alreadyQueued.Contains(x.Id))
            .ToArrayAsync(ct);

        var employeeIds = confirmed.Select(x => x.EmployeeId!.Value).Distinct().ToArray();
        var employees = await db.Employees.AsNoTracking()
            .Where(x => employeeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);

        var now = DateTime.UtcNow;
        var queuedCount = 0;
        foreach (var document in confirmed)
        {
            if (!employees.TryGetValue(document.EmployeeId!.Value, out var employee)) continue;
            if (!employee.IsActive) continue;
            if (employee.NormalizedPhoneNumber is not { Length: > 0 } phone || !phone.StartsWith('+')) continue;

            var outbound = new WhatsAppOutboundMessage(null, null, actorUserId, condominiumId, phone,
                WhatsAppNotificationType.EmployeePayslipAvailable, WhatsAppSendMode.Template,
                $"employee-document:{document.Id}:whatsapp:1", "Holerite disponível.",
                template.Name, template.Language, now, employeeDocumentId: document.Id);
            db.WhatsAppOutboundMessages.Add(outbound);
            db.EmployeeDocumentDeliveries.Add(new EmployeeDocumentDelivery(document.Id, employee.Id, condominiumId,
                batchId, EmployeeDocumentDeliveryChannel.WhatsApp, outbound.Id, actorUserId, now));
            queuedCount++;
        }

        batch.StartDistributing();
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // A concurrent send already queued (or is queuing) these same documents —
            // the unique (EmployeeDocumentId, Channel) index rejected the duplicate
            // insert. Treat as an idempotent no-op rather than an error.
            return 0;
        }

        // Nothing eligible to queue (e.g. every confirmed document already had a
        // delivery, or none had a usable phone) leaves no Pending message behind
        // to ever trigger a completion check later — evaluate right here so the
        // batch doesn't sit in Distributing forever with nothing left to do.
        await TryCompleteBatchAsync(batchId, ct);
        return queuedCount;
    }

    public async Task<IReadOnlyList<EmployeeDocumentDeliveryResponse>> ListDeliveriesAsync(
        Guid condominiumId, Guid batchId, CancellationToken ct)
    {
        var rows = await (from delivery in db.EmployeeDocumentDeliveries.AsNoTracking()
            join outbound in db.WhatsAppOutboundMessages.AsNoTracking() on delivery.OutboundMessageId equals outbound.Id
            join employee in db.Employees.AsNoTracking() on delivery.EmployeeId equals employee.Id
            where delivery.BatchId == batchId && delivery.CondominiumId == condominiumId
            select new EmployeeDocumentDeliveryResponse(delivery.EmployeeDocumentId, delivery.EmployeeId, employee.FullName,
                outbound.Status.ToString(), delivery.AttemptCount, delivery.QueuedAt, outbound.SentAt, outbound.DeliveredAt,
                outbound.ReadAt, outbound.FailedAt, outbound.LastErrorCode, outbound.LastErrorDescription))
            .ToArrayAsync(ct);
        return rows;
    }

    public async Task<bool> RetryAsync(Guid condominiumId, Guid documentId, Guid actorUserId, CancellationToken ct)
    {
        var delivery = await db.EmployeeDocumentDeliveries
            .SingleOrDefaultAsync(x => x.EmployeeDocumentId == documentId && x.CondominiumId == condominiumId, ct);
        if (delivery is null) return false;
        var outbound = await db.WhatsAppOutboundMessages.SingleAsync(x => x.Id == delivery.OutboundMessageId, ct);
        if (outbound.Status is not (WhatsAppOutboundStatus.Failed or WhatsAppOutboundStatus.PermanentlyFailed))
            return false;

        var document = await db.EmployeeDocuments.AsNoTracking().SingleAsync(x => x.Id == documentId, ct);
        var batch = await db.EmployeeDocumentBatches.SingleAsync(x => x.Id == delivery.BatchId, ct);
        var template = whatsAppOptions.Value.Templates.EmployeePayslipAvailable;
        var now = DateTime.UtcNow;
        var newOutbound = new WhatsAppOutboundMessage(null, null, actorUserId, condominiumId, outbound.DestinationPhone,
            WhatsAppNotificationType.EmployeePayslipAvailable, WhatsAppSendMode.Template,
            $"employee-document:{documentId}:whatsapp:{delivery.AttemptCount + 1}", "Holerite disponível.",
            template.Name, template.Language, now, employeeDocumentId: document.Id);
        db.WhatsAppOutboundMessages.Add(newOutbound);
        delivery.Retry(newOutbound.Id, actorUserId, now);
        // A manual resend on an already-Completed batch means it isn't fully at
        // rest anymore — this same method (StartDistributing) already allows
        // Confirmed/Distributing; extending it to Completed lets the batch
        // return to Distributing here without a separate transition, and the
        // usual completion check (webhook/worker) flips it back once this new
        // attempt reaches a terminal status. The previous attempt's row is
        // untouched — history is preserved, never overwritten.
        batch.StartDistributing();
        await db.SaveChangesAsync(ct);
        return true;
    }
}
