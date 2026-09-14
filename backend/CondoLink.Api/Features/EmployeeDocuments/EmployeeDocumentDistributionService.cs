using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.EmployeeDocuments;

public sealed class EmployeeDocumentDistributionService(AppDbContext db, IOptions<WhatsAppOptions> whatsAppOptions)
{
    public async Task<bool> TryCompleteBatchAsync(Guid batchId, CancellationToken ct)
    {
        var batch = await db.EmployeeDocumentBatches.SingleOrDefaultAsync(x => x.Id == batchId && x.DeletedAt == null, ct);
        if (batch is null || batch.Status != EmployeeDocumentBatchStatus.Distributing) return false;
        var pending = await (from d in db.EmployeeDocumentDeliveries.AsNoTracking() join o in db.WhatsAppOutboundMessages.AsNoTracking() on d.OutboundMessageId equals o.Id where d.BatchId == batchId select o.Status).AnyAsync(x => x == WhatsAppOutboundStatus.Pending || x == WhatsAppOutboundStatus.Processing, ct);
        if (pending) return false; batch.MarkCompleted(); await db.SaveChangesAsync(ct); return true;
    }

    public async Task<EmployeeDocumentDistributionSummaryResponse> GetSummaryAsync(Guid batchId, CancellationToken ct)
    {
        var confirmed = await db.EmployeeDocuments.AsNoTracking().Where(x => x.BatchId == batchId && x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed && x.DeletedAt == null).Select(x => new { x.Id, x.EmployeeId }).ToArrayAsync(ct);
        var employees = await db.Employees.AsNoTracking().Where(x => confirmed.Select(d => d.EmployeeId!.Value).Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var queued = (await db.EmployeeDocumentDeliveries.AsNoTracking().Where(x => x.BatchId == batchId).Select(x => x.EmployeeDocumentId).ToArrayAsync(ct)).ToHashSet(); int ready=0,noPhone=0,invalid=0,already=0;
        foreach(var d in confirmed) { if(queued.Contains(d.Id)){already++;continue;} if(!employees.TryGetValue(d.EmployeeId!.Value,out var e)||!e.IsActive||string.IsNullOrWhiteSpace(e.NormalizedPhoneNumber)){noPhone++;continue;} if(!e.NormalizedPhoneNumber.StartsWith('+')){invalid++;continue;} ready++; }
        return new(batchId, confirmed.Length, ready, noPhone, invalid, already);
    }

    public async Task<int> DistributeAsync(Guid batchId, Guid actorUserId, Guid managementCompanyId, CancellationToken ct)
    {
        await using var transaction = await EmployeeDocumentBatchLock.AcquireAsync(db, batchId, ct);
        var batch = await db.EmployeeDocumentBatches.SingleAsync(x => x.Id == batchId && x.ManagementCompanyId == managementCompanyId && x.DeletedAt == null, ct);
        if (batch.Status is not (EmployeeDocumentBatchStatus.Confirmed or EmployeeDocumentBatchStatus.Distributing or EmployeeDocumentBatchStatus.Completed)) throw new InvalidOperationException("Batch must be confirmed before distribution.");
        if (!await db.ManagementCompanyModules.AsNoTracking().AnyAsync(x => x.ManagementCompanyId == managementCompanyId && x.Module == ManagementCompanyModuleType.EmployeeManagement && x.IsEnabled, ct)) throw new InvalidOperationException("Employee Management is disabled.");
        var template=whatsAppOptions.Value.Templates.EmployeePayslipAvailable; if(string.IsNullOrWhiteSpace(template.Name)||string.IsNullOrWhiteSpace(template.Language)) throw new InvalidOperationException("Payslip WhatsApp template is not configured.");
        var queued=(await db.EmployeeDocumentDeliveries.AsNoTracking().Where(x=>x.BatchId==batchId).Select(x=>x.EmployeeDocumentId).ToArrayAsync(ct)).ToHashSet();
        var docs=await db.EmployeeDocuments.Where(x=>x.BatchId==batchId&&x.IdentificationStatus==EmployeeDocumentIdentificationStatus.Confirmed&&x.DeletedAt==null&&!queued.Contains(x.Id)).ToArrayAsync(ct); var employees=await db.Employees.AsNoTracking().Where(x=>docs.Select(d=>d.EmployeeId!.Value).Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct); var now=DateTime.UtcNow; var count=0;
        foreach(var d in docs){ if(!employees.TryGetValue(d.EmployeeId!.Value,out var e)||!e.IsActive||d.CondominiumId!=e.CondominiumId||e.NormalizedPhoneNumber is not {Length:>0} phone||!phone.StartsWith('+'))continue; var c=await db.Condominiums.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==e.CondominiumId&&x.ManagementCompanyId==managementCompanyId&&x.IsActive,ct); if(c is null||!await db.EmployeeDocumentBatchEmployees.AnyAsync(x=>x.BatchId==batchId&&x.EmployeeId==e.Id,ct))continue; var o=new WhatsAppOutboundMessage(null,null,actorUserId,e.CondominiumId,phone,WhatsAppNotificationType.EmployeePayslipAvailable,WhatsAppSendMode.Template,$"employee-document:{d.Id}:whatsapp:1","Holerite disponível.",template.Name,template.Language,now,employeeDocumentId:d.Id); db.WhatsAppOutboundMessages.Add(o); db.EmployeeDocumentDeliveries.Add(new EmployeeDocumentDelivery(d.Id,e.Id,e.CondominiumId,batchId,EmployeeDocumentDeliveryChannel.WhatsApp,o.Id,actorUserId,now)); count++; }
        batch.StartDistributing(); try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){return 0;} await TryCompleteBatchAsync(batchId,ct); await transaction.CommitAsync(ct); return count;
    }
    public async Task<IReadOnlyList<EmployeeDocumentDeliveryResponse>> ListDeliveriesAsync(Guid batchId,CancellationToken ct) => await (from d in db.EmployeeDocumentDeliveries.AsNoTracking() join o in db.WhatsAppOutboundMessages.AsNoTracking() on d.OutboundMessageId equals o.Id join e in db.Employees.AsNoTracking() on d.EmployeeId equals e.Id where d.BatchId==batchId select new EmployeeDocumentDeliveryResponse(d.EmployeeDocumentId,d.EmployeeId,e.FullName,o.Status.ToString(),d.AttemptCount,d.QueuedAt,o.SentAt,o.DeliveredAt,o.ReadAt,o.FailedAt,o.LastErrorCode,o.LastErrorDescription)).ToArrayAsync(ct);
    public async Task<bool> RetryAsync(Guid batchId,Guid documentId,Guid actorUserId,Guid managementCompanyId,CancellationToken ct){await using var transaction=await EmployeeDocumentBatchLock.AcquireAsync(db,batchId,ct);var batch=await db.EmployeeDocumentBatches.SingleOrDefaultAsync(x=>x.Id==batchId&&x.ManagementCompanyId==managementCompanyId&&x.DeletedAt==null,ct);if(batch is null)return false;var d=await db.EmployeeDocumentDeliveries.SingleOrDefaultAsync(x=>x.BatchId==batchId&&x.EmployeeDocumentId==documentId,ct);if(d is null)return false;var o=await db.WhatsAppOutboundMessages.SingleAsync(x=>x.Id==d.OutboundMessageId,ct);if(o.Status is not(WhatsAppOutboundStatus.Failed or WhatsAppOutboundStatus.PermanentlyFailed))return false;var doc=await db.EmployeeDocuments.SingleAsync(x=>x.Id==documentId&&x.BatchId==batchId,ct);var e=await db.Employees.SingleAsync(x=>x.Id==d.EmployeeId);if(doc.DeletedAt is not null||doc.EmployeeId!=e.Id||doc.CondominiumId!=e.CondominiumId||!await db.Condominiums.AnyAsync(c=>c.Id==e.CondominiumId&&c.ManagementCompanyId==managementCompanyId&&c.IsActive,ct))return false;var t=whatsAppOptions.Value.Templates.EmployeePayslipAvailable;var n=new WhatsAppOutboundMessage(null,null,actorUserId,e.CondominiumId,o.DestinationPhone,WhatsAppNotificationType.EmployeePayslipAvailable,WhatsAppSendMode.Template,$"employee-document:{documentId}:whatsapp:{d.AttemptCount+1}","Holerite disponível.",t.Name,t.Language,DateTime.UtcNow,employeeDocumentId:documentId);db.WhatsAppOutboundMessages.Add(n);d.Retry(n.Id,actorUserId,DateTime.UtcNow);batch.StartDistributing();await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return true;}
}
