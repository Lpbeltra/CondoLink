using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class UpdateEmployeeDocumentAssociation
{
    public static IEndpointRouteBuilder MapUpdateEmployeeDocumentAssociation(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/administrator/employees/documents/batches/{batchId:guid}/documents/{documentId:guid}", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeDocuments")
            .WithSummary("Assign, clear, ignore or confirm one document's employee association");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid batchId, Guid documentId, UpdateEmployeeDocumentAssociationRequest request,
        ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, CancellationToken ct)
    {
        var (_, batch) = await access.RequireBatchAsync(principal, batchId, ct);

        var document = await db.EmployeeDocuments
            .SingleOrDefaultAsync(x => x.Id == documentId && x.BatchId == batchId, ct);
        if (document is null) return Results.NotFound(new { message = "Documento não encontrado." });

        if (batch.Status != EmployeeDocumentBatchStatus.ReadyForReview)
            return Results.Conflict(new { message = "Este lote não está mais disponível para revisão." });

        var now = DateTime.UtcNow;
        try
        {
            switch (request.Action)
            {
                case "Assign":
                    if (request.EmployeeId is not Guid employeeId)
                        return Results.BadRequest(new { message = "Informe o funcionário." });
                    // A manual correction may never widen the immutable subset chosen
                    // when the batch was created. This is also an IDOR boundary: an
                    // otherwise in-scope employee cannot be injected into this batch.
                    var exists = await db.EmployeeDocumentBatchEmployees.AsNoTracking()
                        .AnyAsync(x => x.BatchId == batch.Id && x.EmployeeId == employeeId
                            && db.Employees.Any(e => e.Id == employeeId && db.Condominiums.Any(c => c.Id == e.CondominiumId && c.ManagementCompanyId == batch.ManagementCompanyId && c.IsActive)), ct);
                    if (!exists) return Results.BadRequest(new { message = "Funcionário não encontrado neste condomínio." });
                    document.AssignManually(employeeId, now);
                    document.SetCondominium(await db.Employees.Where(x => x.Id == employeeId).Select(x => x.CondominiumId).SingleAsync(ct));
                    break;
                case "Clear":
                    document.ClearAssociation(now);
                    break;
                case "Ignore":
                    document.Ignore(now);
                    break;
                case "Confirm":
                    document.Confirm(now);
                    break;
                default:
                    return Results.BadRequest(new { message = "Ação inválida." });
            }
        }
        catch (InvalidOperationException exception)
        {
            return Results.Conflict(new { message = exception.Message });
        }

        await db.SaveChangesAsync(ct);
        return Results.Ok(new EmployeeDocumentResponse(document.Id, document.BatchId, document.EmployeeId, null,
            document.DocumentType.ToDisplay(), document.CompetenceMonth, document.CompetenceYear,
            document.OriginalFileName, document.PageStart, document.PageEnd,
            document.IdentificationStatus.ToString(), document.IdentificationConfidence.ToString(),
            document.IdentificationMethod.ToString(), document.CreatedAt, document.UpdatedAt, document.ConfirmedAt, false));
    }
}
