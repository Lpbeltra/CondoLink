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
            join c in db.Condominiums.AsNoTracking() on e.CondominiumId equals c.Id
            select new { d, e, c }).AnyAsync(x => x.d.IdentificationStatus != EmployeeDocumentIdentificationStatus.Confirmed
                || x.d.CondominiumId != x.e.CondominiumId
                // Defense in depth: catches drift between match time and confirm time
                // (e.g. the employee's CPF was corrected, or the document's own content
                // was always inconsistent with the employee/condominium it landed on).
                || (x.d.ExtractedCpfDigits != null && x.d.ExtractedCpfDigits != x.e.NormalizedCpf)
                || (x.d.ExtractedCnpjDigits != null && x.d.ExtractedCnpjDigits != x.c.Cnpj)
                || !db.EmployeeDocumentBatchEmployees.Any(b => b.BatchId == batchId && b.EmployeeId == x.e.Id)
                || !db.Condominiums.Any(cc => cc.Id == x.e.CondominiumId && cc.ManagementCompanyId == batch.ManagementCompanyId && cc.IsActive), ct);
        var pending = await db.EmployeeDocuments.AsNoTracking().AnyAsync(x => x.BatchId == batchId && x.IdentificationStatus != EmployeeDocumentIdentificationStatus.Confirmed && x.IdentificationStatus != EmployeeDocumentIdentificationStatus.Ignored, ct);
        if (invalid || pending) return Results.Conflict(new { message = "Revise ou ignore todos os documentos antes de confirmar." });
        batch.Confirm(actor.UserId, DateTime.UtcNow); await db.SaveChangesAsync(ct); return Results.NoContent();
    }
}
