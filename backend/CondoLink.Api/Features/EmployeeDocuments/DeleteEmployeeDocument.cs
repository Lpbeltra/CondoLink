using System.Security.Claims;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

/// <summary>
/// Soft-deletes one already-processed payslip document from normal management —
/// it disappears from listings/review/distribution but its row, and every
/// EmployeeDocumentDelivery/WhatsAppOutboundMessage referencing it, are kept
/// intact for audit and payroll-history purposes. Never a hard delete.
/// </summary>
public static class DeleteEmployeeDocument
{
    public static IEndpointRouteBuilder MapDeleteEmployeeDocument(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/administrator/employees/documents/batches/{batchId:guid}/documents/{documentId:guid}", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeDocuments")
            .WithSummary("Soft-delete one payslip document from management; delivery history is preserved");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid batchId, Guid documentId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, LocalFileStorage storage,
        ILogger<EmployeeDocumentProcessingService> logger, CancellationToken ct)
    {
        var (actor, _) = await access.RequireBatchAsync(principal, batchId, ct);
        await using var transaction = await EmployeeDocumentBatchLock.AcquireAsync(db, batchId, ct);
        // The authorization read precedes the lock; re-read after acquiring it
        // so a concurrent batch delete cannot leave this document actionable.
        var batch = await db.EmployeeDocumentBatches.SingleOrDefaultAsync(x => x.Id == batchId && x.DeletedAt == null, ct);
        if (batch is null) return Results.NotFound(new { message = "Lote não encontrado." });

        var document = await db.EmployeeDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.BatchId == batchId, ct);
        if (document is null || document.DeletedAt is not null)
            return Results.NotFound(new { message = "Documento não encontrado." }); // idempotent: repeat DELETE also 404s

        // The batch as a whole may still be Distributing (other documents in
        // flight) — what matters for THIS document is only its own delivery: a
        // delivery still actively in flight (queued/processing on the WhatsApp
        // side) must finish or fail before its document can be removed.
        var delivery = await db.EmployeeDocumentDeliveries.AsNoTracking().SingleOrDefaultAsync(x => x.EmployeeDocumentId == documentId, ct);
        if (delivery is not null)
        {
            var outboundStatus = await db.WhatsAppOutboundMessages.AsNoTracking()
                .Where(x => x.Id == delivery.OutboundMessageId).Select(x => x.Status).SingleAsync(ct);
            if (outboundStatus is WhatsAppOutboundStatus.Pending or WhatsAppOutboundStatus.Processing)
                return Results.Conflict(new { message = "O envio deste holerite está em andamento; aguarde a conclusão para excluir." });
        }

        try { document.SoftDelete(actor.UserId, DateTime.UtcNow); }
        catch (InvalidOperationException exception) { return Results.Conflict(new { message = exception.Message }); }

        // Database first: the file is only removed once the soft-delete itself is
        // durably committed, never before.
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        if (document.FileKey != "pending")
        {
            try { storage.Delete(document.FileKey); }
            catch (Exception exception)
            {
                // The record is already safely soft-deleted; a storage-layer problem
                // (e.g. permissions, already-gone file) must not surface as a failed
                // request — log without any salary/CPF/file-name content and move on.
                logger.LogWarning(exception, "Failed to remove employee document file from storage. DocumentId: {DocumentId}; BatchId: {BatchId}.", documentId, batchId);
            }
        }

        logger.LogInformation(
            "Employee payslip document deleted. ManagementCompanyId: {ManagementCompanyId}; BatchId: {BatchId}; DocumentId: {DocumentId}; EmployeeId: {EmployeeId}; ActorUserId: {ActorUserId}.",
            batch.ManagementCompanyId, batchId, documentId, document.EmployeeId, actor.UserId);

        return Results.NoContent();
    }
}
