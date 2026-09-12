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
        endpoints.MapGet("/condominiums/{condominiumId:guid}/employees/documents/batches", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeDocuments")
            .WithSummary("List payslip document batches for a condominium");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, CancellationToken ct)
    {
        await access.RequireAsync(principal, condominiumId, ct);

        var batches = await db.EmployeeDocumentBatches.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId)
            .OrderByDescending(x => x.CreatedAt)
            .ToArrayAsync(ct);

        var userIds = batches.Select(x => x.CreatedByUserId)
            .Concat(batches.Where(x => x.ConfirmedByUserId.HasValue).Select(x => x.ConfirmedByUserId!.Value))
            .Distinct().ToArray();
        var names = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(x => userIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.FullName, ct);

        var documentCounts = await db.EmployeeDocuments.AsNoTracking()
            .Where(x => x.CondominiumId == condominiumId)
            .GroupBy(x => x.BatchId)
            .Select(g => new
            {
                BatchId = g.Key,
                Total = g.Count(),
                Identified = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Identified),
                NeedsReview = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.NeedsReview),
                Unidentified = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Unidentified),
                Ignored = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Ignored),
                Confirmed = g.Count(x => x.IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed),
            }).ToDictionaryAsync(x => x.BatchId, ct);

        var response = batches.Select(batch =>
        {
            documentCounts.TryGetValue(batch.Id, out var counts);
            return new EmployeeDocumentBatchResponse(batch.Id, batch.CondominiumId, batch.DocumentType.ToDisplay(),
                batch.CompetenceMonth, batch.CompetenceYear, batch.Status.ToString(), batch.CreatedAt,
                names.GetValueOrDefault(batch.CreatedByUserId, "—"), batch.ConfirmedAt,
                batch.ConfirmedByUserId is Guid confirmedBy ? names.GetValueOrDefault(confirmedBy, "—") : null,
                batch.FailureReason, counts?.Total ?? 0, counts?.Identified ?? 0, counts?.NeedsReview ?? 0,
                counts?.Unidentified ?? 0, counts?.Ignored ?? 0, counts?.Confirmed ?? 0);
        }).ToArray();

        return Results.Ok(response);
    }
}
