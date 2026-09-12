using System.Security.Claims;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class PreviewEmployeeDocument
{
    public static IEndpointRouteBuilder MapPreviewEmployeeDocument(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/condominiums/{condominiumId:guid}/employees/documents/{documentId:guid}/preview", HandleAsync)
            .RequireAuthorization()
            .WithTags("EmployeeDocuments")
            .WithSummary("Stream one individualized document's PDF content — never a public URL");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, Guid documentId, ClaimsPrincipal principal, AppDbContext db,
        EmployeeManagement.EmployeeManagementAccessService access, LocalFileStorage storage, CancellationToken ct)
    {
        // Re-checked on every request: an administradora that lost access to this
        // condominium (or a SubManager whose permission was revoked) between
        // listing documents and opening a preview link must not be able to stream it.
        await access.RequireAsync(principal, condominiumId, ct);

        var document = await db.EmployeeDocuments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == documentId && x.CondominiumId == condominiumId, ct);
        if (document is null) return Results.NotFound(new { message = "Documento não encontrado." });

        var stream = storage.OpenRead(document.FileKey);
        if (stream is null) return Results.NotFound(new { message = "Arquivo não encontrado." });
        return Results.File(stream, "application/pdf", enableRangeProcessing: true);
    }
}
