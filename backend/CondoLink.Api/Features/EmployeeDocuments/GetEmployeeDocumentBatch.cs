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
        endpoints.MapGet("/administrator/employees/documents/batches/{batchId:guid}", HandleAsync).RequireAuthorization().WithTags("EmployeeDocuments");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, CancellationToken ct)
    {
        var (_, batch) = await access.RequireBatchAsync(principal, batchId, ct);
        var documents = await db.EmployeeDocuments.AsNoTracking().Where(x => x.BatchId == batchId && x.DeletedAt == null).OrderBy(x => x.PageStart).ToArrayAsync(ct);
        var employeeIds = documents.Where(x => x.EmployeeId.HasValue).Select(x => x.EmployeeId!.Value).Distinct().ToArray();
        var employees = await db.Employees.AsNoTracking().Where(x => employeeIds.Contains(x.Id))
            .Join(db.Condominiums.AsNoTracking(), e => e.CondominiumId, c => c.Id, (e, c) => new { e.Id, e.FullName, e.Cpf, e.RegistrationNumber, CondominiumId = c.Id, CondominiumName = c.Name }).ToDictionaryAsync(x => x.Id, ct);
        var response = documents.Select(d => new { d.Id, d.BatchId, d.EmployeeId, employeeName = d.EmployeeId is Guid id ? employees.GetValueOrDefault(id)?.FullName : null, condominiumId = d.EmployeeId is Guid eid ? employees.GetValueOrDefault(eid)?.CondominiumId : d.CondominiumId, condominiumName = d.EmployeeId is Guid eeid ? employees.GetValueOrDefault(eeid)?.CondominiumName : null, cpf = d.EmployeeId is Guid ceid && employees.TryGetValue(ceid, out var e) && e.Cpf is { Length: >= 2 } cpf ? $"***.***.***-{cpf[^2..]}" : null, d.DocumentType, d.CompetenceMonth, d.CompetenceYear, d.OriginalFileName, d.PageStart, d.PageEnd, identificationStatus = d.IdentificationStatus.ToString(), identificationConfidence = d.IdentificationConfidence.ToString(), identificationMethod = d.IdentificationMethod.ToString(), d.CreatedAt, d.UpdatedAt, d.ConfirmedAt }).ToArray();
        var selectedCount = await db.EmployeeDocumentBatchEmployees.CountAsync(x => x.BatchId == batchId, ct);
        var condominiumCount = employees.Values.Select(x => x.CondominiumId).Distinct().Count();
        return Results.Ok(new { batch = new { batch.Id, batch.ManagementCompanyId, batch.DocumentType, batch.CompetenceMonth, batch.CompetenceYear, status = batch.Status.ToString(), batch.CreatedAt, batch.ProcessingStage, batch.ProcessedItems, batch.TotalItems, batch.ProgressPercentage, selectedCount, condominiumCount }, documents = response });
    }
}
