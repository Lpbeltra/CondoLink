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
            .SingleOrDefaultAsync(x => x.Id == documentId && x.BatchId == batchId && x.DeletedAt == null, ct);
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
                    // Authorization boundary: an employee is assignable only if they
                    // currently belong to a condominium this management company
                    // administers — never anything pre-selected, never cross-company.
                    var candidate = await db.Employees.AsNoTracking()
                        .Where(e => e.Id == employeeId && db.Condominiums.Any(c => c.Id == e.CondominiumId && c.ManagementCompanyId == batch.ManagementCompanyId && c.IsActive))
                        .Select(e => new { e.CondominiumId, e.NormalizedCpf })
                        .SingleOrDefaultAsync(ct);
                    if (candidate is null) return Results.BadRequest(new { message = "Funcionário não encontrado neste condomínio." });
                    // Hard gates (§27): a document's own extracted CPF/CNPJ can never be
                    // manually overridden. If the PDF explicitly names a different person
                    // or a different condominium, the fix is to replace the file — not to
                    // force an association the document's own content contradicts.
                    if (document.ExtractedCpfDigits is { Length: > 0 } docCpf && docCpf != candidate.NormalizedCpf)
                        return Results.BadRequest(new { message = "O CPF encontrado no documento não corresponde ao funcionário selecionado." });
                    var condominiumCnpj = await db.Condominiums.AsNoTracking().Where(c => c.Id == candidate.CondominiumId).Select(c => c.Cnpj).SingleAsync(ct);
                    if (document.ExtractedCnpjDigits is { Length: > 0 } docCnpj && docCnpj != condominiumCnpj)
                        return Results.BadRequest(new { message = "O CNPJ encontrado no documento pertence a outro condomínio." });
                    document.AssignManually(employeeId, now);
                    document.SetCondominium(candidate.CondominiumId);
                    // Populate the batch's employee scope as associations are made — see
                    // EmployeeDocumentProcessingService for the automatic-match side of this.
                    if (!await db.EmployeeDocumentBatchEmployees.AnyAsync(x => x.BatchId == batch.Id && x.EmployeeId == employeeId, ct))
                        db.EmployeeDocumentBatchEmployees.Add(new CondoLink.Domain.Entities.EmployeeDocumentBatchEmployee(batch.Id, employeeId));
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
