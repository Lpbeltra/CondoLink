using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class UpdateEmployeeDocumentAssociation
{
    public static IEndpointRouteBuilder MapUpdateEmployeeDocumentAssociation(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPatch("/condominiums/{condominiumId:guid}/employees/documents/{documentId:guid}", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeDocuments")
            .WithSummary("Assign, clear, ignore or confirm one document's employee association");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, Guid documentId, UpdateEmployeeDocumentAssociationRequest request,
        ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, CancellationToken ct)
    {
        await access.RequireAsync(principal, condominiumId, ct);

        var document = await db.EmployeeDocuments
            .SingleOrDefaultAsync(x => x.Id == documentId && x.CondominiumId == condominiumId, ct);
        if (document is null) return Results.NotFound(new { message = "Documento não encontrado." });

        var batch = await db.EmployeeDocumentBatches
            .SingleOrDefaultAsync(x => x.Id == document.BatchId && x.CondominiumId == condominiumId, ct);
        if (batch is null || batch.Status != EmployeeDocumentBatchStatus.ReadyForReview)
            return Results.Conflict(new { message = "Este lote não está mais disponível para revisão." });

        var now = DateTime.UtcNow;
        try
        {
            switch (request.Action)
            {
                case "Assign":
                    if (request.EmployeeId is not Guid employeeId)
                        return Results.BadRequest(new { message = "Informe o funcionário." });
                    var exists = await db.Employees.AsNoTracking()
                        .AnyAsync(x => x.Id == employeeId && x.CondominiumId == condominiumId, ct);
                    if (!exists) return Results.BadRequest(new { message = "Funcionário não encontrado neste condomínio." });
                    document.AssignManually(employeeId, now);
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
