using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class ListEmployeeDocumentBatches
{
    public static IEndpointRouteBuilder MapListEmployeeDocumentBatches(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/administrator/employees/documents/batches", HandleAsync).RequireAuthorization().WithTags("EmployeeDocuments");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, CancellationToken ct)
    {
        var scope = await access.RequireAdministratorAsync(principal, ct);
        var batches = await db.EmployeeDocumentBatches.AsNoTracking().Where(x => x.ManagementCompanyId == scope.ManagementCompanyId)
            .OrderByDescending(x => x.CreatedAt).ToArrayAsync(ct);
        var ids = batches.Select(x => x.CreatedByUserId).Concat(batches.Where(x => x.ConfirmedByUserId.HasValue).Select(x => x.ConfirmedByUserId!.Value)).Distinct().ToArray();
        var names = await db.Set<ApplicationUser>().AsNoTracking().Where(x => ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        var batchIds = batches.Select(x => x.Id).ToArray();
        var counts = await db.EmployeeDocuments.AsNoTracking().Where(x => batchIds.Contains(x.BatchId) && x.DeletedAt == null).GroupBy(x => x.BatchId)
            .Select(g => new { BatchId = g.Key, Total = g.Count(), Identified = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Identified), NeedsReview = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.NeedsReview), Unidentified = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Unidentified), Ignored = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Ignored), Confirmed = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed) }).ToDictionaryAsync(x => x.BatchId, ct);
        return Results.Ok(batches.Select(b => { counts.TryGetValue(b.Id, out var c); return new EmployeeDocumentBatchResponse(b.Id, null, b.DocumentType.ToDisplay(), b.CompetenceMonth, b.CompetenceYear, b.Status.ToString(), b.CreatedAt, names.GetValueOrDefault(b.CreatedByUserId, "—"), b.ConfirmedAt, b.ConfirmedByUserId is Guid id ? names.GetValueOrDefault(id, "—") : null, b.FailureReason, c?.Total ?? 0, c?.Identified ?? 0, c?.NeedsReview ?? 0, c?.Unidentified ?? 0, c?.Ignored ?? 0, c?.Confirmed ?? 0, b.ProcessingStage, b.ProcessedItems, b.TotalItems, b.ProgressPercentage); }).ToArray());
    }
}
