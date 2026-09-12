using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class GetEmployeeDocumentBatch
{
    public static IEndpointRouteBuilder MapGetEmployeeDocumentBatch(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/employees/documents/batches/{batchId:guid}", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeDocuments")
            .WithSummary("Get a payslip batch and its individualized documents for review");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, CancellationToken ct)
    {
        await access.RequireAsync(principal, condominiumId, ct);

        var batch = await db.EmployeeDocumentBatches.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == batchId && x.CondominiumId == condominiumId, ct);
        if (batch is null) return Results.NotFound(new { message = "Lote não encontrado." });

        var documents = await db.EmployeeDocuments.AsNoTracking()
            .Where(x => x.BatchId == batchId && x.CondominiumId == condominiumId)
            .OrderBy(x => x.PageStart)
            .ToArrayAsync(ct);

        var employeeIds = documents.Where(x => x.EmployeeId.HasValue).Select(x => x.EmployeeId!.Value).Distinct().ToArray();
        var employeeNames = await db.Employees.AsNoTracking()
            .Where(x => employeeIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        // A duplicate is the same rendered content for the same employee/competence/type
        // confirmed in a DIFFERENT, earlier batch — never within this batch itself.
        var duplicateHashes = (await db.EmployeeDocuments.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId && x.BatchId != batchId
                && x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed
                && x.DocumentType == batch.DocumentType && x.CompetenceMonth == batch.CompetenceMonth
                && x.CompetenceYear == batch.CompetenceYear)
            .Select(x => new { x.EmployeeId, x.ContentHash }).ToArrayAsync(ct))
            .Select(x => (x.EmployeeId, x.ContentHash)).ToHashSet();

        var response = documents.Select(document => new EmployeeDocumentResponse(
            document.Id, document.BatchId, document.EmployeeId,
            document.EmployeeId is Guid employeeId ? employeeNames.GetValueOrDefault(employeeId) : null,
            document.DocumentType.ToDisplay(), document.CompetenceMonth, document.CompetenceYear,
            document.OriginalFileName, document.PageStart, document.PageEnd,
            document.IdentificationStatus.ToString(), document.IdentificationConfidence.ToString(),
            document.IdentificationMethod.ToString(), document.CreatedAt, document.UpdatedAt, document.ConfirmedAt,
            document.EmployeeId is Guid id && duplicateHashes.Contains((id, document.ContentHash))
        )).ToArray();

        var createdByName = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(x => x.Id == batch.CreatedByUserId).Select(x => x.FullName).SingleOrDefaultAsync(ct) ?? "—";
        string? confirmedByName = null;
        if (batch.ConfirmedByUserId is Guid confirmedBy)
            confirmedByName = await db.Set<ApplicationUser>().AsNoTracking()
                .Where(x => x.Id == confirmedBy).Select(x => x.FullName).SingleOrDefaultAsync(ct);

        var batchResponse = new EmployeeDocumentBatchResponse(batch.Id, batch.CondominiumId, batch.DocumentType.ToDisplay(),
            batch.CompetenceMonth, batch.CompetenceYear, batch.Status.ToString(), batch.CreatedAt, createdByName,
            batch.ConfirmedAt, confirmedByName, batch.FailureReason, documents.Length,
            documents.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Identified),
            documents.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.NeedsReview),
            documents.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Unidentified),
            documents.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Ignored),
            documents.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed));

        return Results.Ok(new { Batch = batchResponse, Documents = response });
    }
}
