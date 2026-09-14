using System.Security.Claims;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class DeleteEmployeeDocumentBatch
{
    private const string ProcessingMessage = "Este lote ainda está sendo processado e não pode ser excluído agora.";
    private const string SendingMessage = "Este lote possui envios em andamento. Aguarde a conclusão antes de excluí-lo.";

    public static IEndpointRouteBuilder MapDeleteEmployeeDocumentBatch(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/administrator/employees/documents/batches/{batchId:guid}", HandleAsync)
            .RequireAuthorization().WithTags("EmployeeDocuments");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, LocalFileStorage storage,
        ILogger<EmployeeDocumentProcessingService> logger, CancellationToken ct)
    {
        var actor = await access.RequireAdministratorAsync(principal, ct);
        await using var transaction = await EmployeeDocumentBatchLock.AcquireAsync(db, batchId, ct);
        var batch = await db.EmployeeDocumentBatches
            .SingleOrDefaultAsync(x => x.Id == batchId && x.ManagementCompanyId == actor.ManagementCompanyId && x.DeletedAt == null, ct);
        if (batch is null) return Results.NotFound(new { message = "Lote não encontrado." });

        if (batch.Status is EmployeeDocumentBatchStatus.Uploaded or EmployeeDocumentBatchStatus.Processing)
            return Results.Conflict(new { message = ProcessingMessage });

        var activeSend = await (from d in db.EmployeeDocumentDeliveries.AsNoTracking()
            join o in db.WhatsAppOutboundMessages.AsNoTracking() on d.OutboundMessageId equals o.Id
            where d.BatchId == batchId
            select o.Status).AnyAsync(x => x == WhatsAppOutboundStatus.Pending || x == WhatsAppOutboundStatus.Processing, ct);
        if (activeSend) return Results.Conflict(new { message = SendingMessage });

        var documents = await db.EmployeeDocuments.Where(x => x.BatchId == batchId).ToArrayAsync(ct);
        var now = DateTime.UtcNow;
        batch.SoftDelete(actor.UserId, now);
        foreach (var document in documents) document.SoftDeleteFromBatch(actor.UserId, now);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        foreach (var document in documents.Where(x => x.FileKey != "pending"))
        {
            try { storage.Delete(document.FileKey); }
            catch (Exception exception)
            {
                logger.LogWarning(exception,
                    "Failed to remove employee batch file from storage. BatchId: {BatchId}; DocumentId: {DocumentId}.",
                    batchId, document.Id);
            }
        }

        logger.LogInformation(
            "Employee payslip batch deleted. ManagementCompanyId: {ManagementCompanyId}; BatchId: {BatchId}; DocumentCount: {DocumentCount}; ActorUserId: {ActorUserId}.",
            actor.ManagementCompanyId, batchId, documents.Length, actor.UserId);
        return Results.NoContent();
    }
}
