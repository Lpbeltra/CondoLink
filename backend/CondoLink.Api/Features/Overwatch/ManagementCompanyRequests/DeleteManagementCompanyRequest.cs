using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyRequests;

public static class DeleteManagementCompanyRequest
{
    public static IEndpointRouteBuilder MapDeleteManagementCompanyRequest(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDelete("/overwatch/management-company-requests/{id:guid}", HandleAsync)
            .RequireAuthorization("PlatformAdmin").WithTags("Overwatch");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid id, [FromBody] Confirmation confirmation, ClaimsPrincipal user, [FromServices] AppDbContext db,
        [FromServices] LocalFileStorage storage, [FromServices] ILoggerFactory loggerFactory, CancellationToken ct)
    {
        var request = await db.ManagementCompanyRequests.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (request is null) return Results.NotFound();
        if (!string.Equals(request.FriendlyIdentifier, confirmation.FriendlyIdentifier, StringComparison.Ordinal))
            return Results.BadRequest(new { message = "A confirmação deve repetir o FriendlyIdentifier exato." });
        var attachments = await db.ManagementCompanyRequestAttachments.Where(x => x.RequestId == id).Select(x => x.StorageKey).ToListAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Notifications.RemoveRange(db.Notifications.Where(x => x.ManagementCompanyRequestId == id));
        db.ManagementCompanyRequestAttachments.RemoveRange(db.ManagementCompanyRequestAttachments.Where(x => x.RequestId == id));
        db.ManagementCompanyRequestMessages.RemoveRange(db.ManagementCompanyRequestMessages.Where(x => x.RequestId == id));
        db.ManagementCompanyRequestHistories.RemoveRange(db.ManagementCompanyRequestHistories.Where(x => x.RequestId == id));
        db.ManagementCompanyFineRequests.RemoveRange(db.ManagementCompanyFineRequests.Where(x => x.RequestId == id));
        db.ManagementCompanyPaymentRequests.RemoveRange(db.ManagementCompanyPaymentRequests.Where(x => x.RequestId == id));
        db.ManagementCompanyGeneralQuestionRequests.RemoveRange(db.ManagementCompanyGeneralQuestionRequests.Where(x => x.RequestId == id));
        db.ManagementCompanyRequests.Remove(request);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        foreach (var key in attachments) storage.Delete(key);
        loggerFactory.CreateLogger("Overwatch.ManagementCompanyRequestDeletion").LogWarning(
            "ManagementCompanyRequest permanently deleted: FriendlyIdentifier={FriendlyIdentifier}; RequestId={RequestId}; CondominiumId={CondominiumId}; ManagementCompanyId={ManagementCompanyId}; Actor={Actor}",
            request.FriendlyIdentifier, request.Id, request.CondominiumId, request.ManagementCompanyId,
            user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub") ?? "unknown");
        return Results.NoContent();
    }

    public sealed record Confirmation(string FriendlyIdentifier);
}
