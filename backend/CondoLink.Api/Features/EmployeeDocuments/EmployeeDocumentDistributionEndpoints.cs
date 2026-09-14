using System.Security.Claims;
using CondoLink.Infrastructure.Persistence;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class EmployeeDocumentDistributionEndpoints
{
    public static IEndpointRouteBuilder MapEmployeeDocumentDistributionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var prefix="/administrator/employees/documents/batches/{batchId:guid}";
        endpoints.MapGet(prefix+"/distribution-summary",SummaryAsync).RequireAuthorization().WithTags("EmployeeDocuments");
        endpoints.MapPost(prefix+"/distribute",DistributeAsync).RequireAuthorization().WithTags("EmployeeDocuments");
        endpoints.MapGet(prefix+"/deliveries",ListAsync).RequireAuthorization().WithTags("EmployeeDocuments");
        endpoints.MapPost(prefix+"/documents/{documentId:guid}/resend",RetryAsync).RequireAuthorization().WithTags("EmployeeDocuments"); return endpoints;
    }
    private static async Task<IResult> SummaryAsync(Guid batchId,ClaimsPrincipal p,AppDbContext db,EmployeeManagement.EmployeeManagementAccessService a,EmployeeDocumentDistributionService s,CancellationToken ct){await a.RequireBatchAsync(p,batchId,ct);return Results.Ok(await s.GetSummaryAsync(batchId,ct));}
    private static async Task<IResult> DistributeAsync(Guid batchId,ClaimsPrincipal p,AppDbContext db,EmployeeManagement.EmployeeManagementAccessService a,EmployeeDocumentDistributionService s,CancellationToken ct){var(actor,b)=await a.RequireBatchAsync(p,batchId,ct);try{return Results.Ok(new{queued=await s.DistributeAsync(batchId,actor.UserId,b.ManagementCompanyId!.Value,ct)});}catch(InvalidOperationException e){return Results.Conflict(new{message=e.Message});}}
    private static async Task<IResult> ListAsync(Guid batchId,ClaimsPrincipal p,AppDbContext db,EmployeeManagement.EmployeeManagementAccessService a,EmployeeDocumentDistributionService s,CancellationToken ct){await a.RequireBatchAsync(p,batchId,ct);return Results.Ok(await s.ListDeliveriesAsync(batchId,ct));}
    private static async Task<IResult> RetryAsync(Guid batchId,Guid documentId,ClaimsPrincipal p,AppDbContext db,EmployeeManagement.EmployeeManagementAccessService a,EmployeeDocumentDistributionService s,CancellationToken ct){var(actor,b)=await a.RequireBatchAsync(p,batchId,ct);return await s.RetryAsync(batchId,documentId,actor.UserId,b.ManagementCompanyId!.Value,ct)?Results.NoContent():Results.Conflict(new{message="Este documento não possui uma entrega reenviável."});}
}
