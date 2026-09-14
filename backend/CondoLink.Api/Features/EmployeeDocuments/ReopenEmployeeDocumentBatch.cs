using System.Security.Claims;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class ReopenEmployeeDocumentBatch
{
    public static IEndpointRouteBuilder MapReopenEmployeeDocumentBatch(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/administrator/employees/documents/batches/{batchId:guid}/reopen", HandleAsync).RequireAuthorization().WithTags("EmployeeDocuments");
        return endpoints;
    }
    private static async Task<IResult> HandleAsync(Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, CancellationToken ct)
    {
        var (_, batch) = await access.RequireBatchAsync(principal, batchId, ct);
        if (await db.EmployeeDocumentDeliveries.AsNoTracking().AnyAsync(x => x.BatchId == batchId, ct)) return Results.Conflict(new { message = "O lote não pode ser reaberto após o início do envio." });
        try { batch.ReopenForReview(); await db.SaveChangesAsync(ct); return Results.Ok(new { batch.Id, Status = batch.Status.ToString() }); }
        catch (InvalidOperationException ex) { return Results.Conflict(new { message = ex.Message }); }
    }
}
