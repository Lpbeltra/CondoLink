using System.Security.Claims;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class EmployeeDocumentDistributionEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeDocumentDistributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/employees/documents/batches/{batchId:guid}/distribution-summary", SummaryAsync)
            .RequireAuthorization().WithTags("EmployeeDocuments")
            .WithSummary("How many confirmed payslips are ready to send via WhatsApp");
        endpoints.MapPost("/condominiums/{condominiumId:guid}/employees/documents/batches/{batchId:guid}/distribute", DistributeAsync)
            .RequireAuthorization().WithTags("EmployeeDocuments")
            .WithSummary("Queue WhatsApp delivery for every ready, confirmed payslip in the batch");
        endpoints.MapGet("/condominiums/{condominiumId:guid}/employees/documents/batches/{batchId:guid}/deliveries", ListDeliveriesAsync)
            .RequireAuthorization().WithTags("EmployeeDocuments")
            .WithSummary("Delivery status per document — sent/delivered/read/failed");
        endpoints.MapPost("/condominiums/{condominiumId:guid}/employees/documents/{documentId:guid}/resend", ResendAsync)
            .RequireAuthorization().WithTags("EmployeeDocuments")
            .WithSummary("Retry a failed WhatsApp delivery for one document");
        return endpoints;
    }

    private static async Task<IResult> SummaryAsync(
        Guid condominiumId, Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access,
        EmployeeDocumentDistributionService distribution, CancellationToken ct)
    {
        await access.RequireAsync(principal, condominiumId, ct);
        var batchExists = await db.EmployeeDocumentBatches.AsNoTracking().AnyAsync(x => x.Id == batchId && x.CondominiumId == condominiumId, ct);
        if (!batchExists) return Results.NotFound(new { message = "Lote não encontrado." });
        return Results.Ok(await distribution.GetSummaryAsync(condominiumId, batchId, ct));
    }

    private static async Task<IResult> DistributeAsync(
        Guid condominiumId, Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access,
        EmployeeDocumentDistributionService distribution, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        // Revalidated here — not trusted from when the batch was confirmed: the
        // administradora may have lost delegation, the module may have been
        // disabled, or this operator's permission may have been revoked since.
        var actor = await access.RequireAsync(principal, condominiumId, ct);

        var batch = await db.EmployeeDocumentBatches.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == batchId && x.CondominiumId == condominiumId, ct);
        if (batch is null) return Results.NotFound(new { message = "Lote não encontrado." });
        // Completed is included: a batch can legitimately finish (all deliveries
        // terminal) between one click and a repeated/racing one. Calling this
        // again on an already-finished batch queues nothing new and settles
        // right back to Completed — a harmless no-op, not an error.
        if (batch.Status is not (EmployeeDocumentBatchStatus.Confirmed or EmployeeDocumentBatchStatus.Distributing
            or EmployeeDocumentBatchStatus.Completed))
            return Results.Conflict(new { message = "Confirme as associações do lote antes de distribuir." });

        int queued;
        try { queued = await distribution.DistributeAsync(condominiumId, batchId, actor.UserId, ct); }
        catch (InvalidOperationException exception) { return Results.Conflict(new { message = exception.Message }); }

        loggerFactory.CreateLogger("EmployeeDocumentAudit").LogInformation(
            "EmployeeDocumentDistributionStarted. CondominiumId: {CondominiumId}; BatchId: {BatchId}; Queued: {Queued}; ActorUserId: {ActorUserId}; ActorType: {ActorType}.",
            condominiumId, batchId, queued, actor.UserId, actor.Kind);

        return Results.Ok(new { Queued = queued });
    }

    private static async Task<IResult> ListDeliveriesAsync(
        Guid condominiumId, Guid batchId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access,
        EmployeeDocumentDistributionService distribution, CancellationToken ct)
    {
        await access.RequireAsync(principal, condominiumId, ct);
        var batchExists = await db.EmployeeDocumentBatches.AsNoTracking().AnyAsync(x => x.Id == batchId && x.CondominiumId == condominiumId, ct);
        if (!batchExists) return Results.NotFound(new { message = "Lote não encontrado." });
        return Results.Ok(await distribution.ListDeliveriesAsync(condominiumId, batchId, ct));
    }

    private static async Task<IResult> ResendAsync(
        Guid condominiumId, Guid documentId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access,
        EmployeeDocumentDistributionService distribution, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var actor = await access.RequireAsync(principal, condominiumId, ct);
        var documentExists = await db.EmployeeDocuments.AsNoTracking().AnyAsync(x => x.Id == documentId && x.CondominiumId == condominiumId, ct);
        if (!documentExists) return Results.NotFound(new { message = "Documento não encontrado." });

        var retried = await distribution.RetryAsync(condominiumId, documentId, actor.UserId, ct);
        if (!retried) return Results.Conflict(new { message = "Este documento não possui uma entrega com falha para reenviar." });

        loggerFactory.CreateLogger("EmployeeDocumentAudit").LogInformation(
            "EmployeeDocumentDeliveryRetried. CondominiumId: {CondominiumId}; DocumentId: {DocumentId}; ActorUserId: {ActorUserId}; ActorType: {ActorType}.",
            condominiumId, documentId, actor.UserId, actor.Kind);
        return Results.NoContent();
    }
}
