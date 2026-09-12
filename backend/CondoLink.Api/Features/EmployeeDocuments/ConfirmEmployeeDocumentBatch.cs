using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class ConfirmEmployeeDocumentBatch
{
    public static IEndpointRouteBuilder MapConfirmEmployeeDocumentBatch(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums/{condominiumId:guid}/employees/documents/batches/{batchId:guid}/confirm", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeDocuments")
            .WithSummary("Finalize a batch's document associations — required before distribution");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var actor = await access.RequireAsync(principal, condominiumId, ct);

        var batch = await db.EmployeeDocumentBatches
            .SingleOrDefaultAsync(x => x.Id == batchId && x.CondominiumId == condominiumId, ct);
        if (batch is null) return Results.NotFound(new { message = "Lote não encontrado." });
        if (batch.Status != EmployeeDocumentBatchStatus.ReadyForReview)
            return Results.Conflict(new { message = "Este lote não está pronto para confirmação." });

        var pending = await db.EmployeeDocuments.AsNoTracking()
            .CountAsync(x => x.BatchId == batchId
                && x.IdentificationStatus != EmployeeDocumentIdentificationStatus.Confirmed
                && x.IdentificationStatus != EmployeeDocumentIdentificationStatus.Ignored, ct);
        if (pending > 0)
            return Results.Conflict(new { message = $"Ainda há {pending} documento(s) sem revisão concluída (confirmar ou ignorar)." });

        batch.Confirm(actor.UserId, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);

        loggerFactory.CreateLogger("EmployeeDocumentAudit").LogInformation(
            "EmployeeDocumentBatchConfirmed. CondominiumId: {CondominiumId}; BatchId: {BatchId}; ActorUserId: {ActorUserId}; ActorType: {ActorType}.",
            condominiumId, batchId, actor.UserId, actor.Kind);

        return Results.NoContent();
    }
}
