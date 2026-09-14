using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class ConfirmEmployeeDocumentBatch
{
    public static IEndpointRouteBuilder MapConfirmEmployeeDocumentBatch(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/administrator/employees/documents/batches/{batchId:guid}/confirm", HandleAsync).RequireAuthorization().WithTags("EmployeeDocuments");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, CancellationToken ct)
    {
        var (actor, batch) = await access.RequireBatchAsync(principal, batchId, ct);
        if (batch.Status != EmployeeDocumentBatchStatus.ReadyForReview) return Results.Conflict(new { message = "Este lote não está pronto para confirmação." });
        var invalid = await (from d in db.EmployeeDocuments.AsNoTracking()
            where d.BatchId == batchId && d.IdentificationStatus != EmployeeDocumentIdentificationStatus.Ignored
            join e in db.Employees.AsNoTracking() on d.EmployeeId equals e.Id
            select new { d, e }).AnyAsync(x => x.d.IdentificationStatus != EmployeeDocumentIdentificationStatus.Confirmed
                || x.d.CondominiumId != x.e.CondominiumId
                || !db.EmployeeDocumentBatchEmployees.Any(b => b.BatchId == batchId && b.EmployeeId == x.e.Id)
                || !db.Condominiums.Any(c => c.Id == x.e.CondominiumId && c.ManagementCompanyId == batch.ManagementCompanyId && c.IsActive), ct);
        var pending = await db.EmployeeDocuments.AsNoTracking().AnyAsync(x => x.BatchId == batchId && x.IdentificationStatus != EmployeeDocumentIdentificationStatus.Confirmed && x.IdentificationStatus != EmployeeDocumentIdentificationStatus.Ignored, ct);
        if (invalid || pending) return Results.Conflict(new { message = "Revise ou ignore todos os documentos antes de confirmar." });
        batch.Confirm(actor.UserId, DateTime.UtcNow); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
}
