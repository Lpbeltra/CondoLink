using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class ReplaceEmployeeDocumentFile
{
    public static IEndpointRouteBuilder MapReplaceEmployeeDocumentFile(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/condominiums/{condominiumId:guid}/employees/documents/{documentId:guid}/file", HandleAsync)
            .RequireAuthorization().DisableAntiforgery().WithTags("EmployeeDocuments");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid condominiumId, Guid documentId, HttpRequest request,
        ClaimsPrincipal principal, AppDbContext db, EmployeeManagement.EmployeeManagementAccessService access,
        LocalFileStorage storage, CancellationToken ct)
    {
        await access.RequireAsync(principal, condominiumId, ct);
        var document = await db.EmployeeDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.CondominiumId == condominiumId, ct);
        if (document is null) return Results.NotFound(new { message = "Documento não encontrado." });
        var batch = await db.EmployeeDocumentBatches.SingleOrDefaultAsync(x => x.Id == document.BatchId && x.CondominiumId == condominiumId, ct);
        if (batch is null) return Results.NotFound(new { message = "Lote não encontrado." });
        if (batch.Status != EmployeeDocumentBatchStatus.ReadyForReview)
            return Results.Conflict(new { message = "O arquivo não pode ser substituído depois do início da distribuição." });
        if (!request.HasFormContentType) return Results.BadRequest(new { message = "Envie um PDF." });
        var file = (await request.ReadFormAsync(ct)).Files.GetFile("file");
        if (file is null) return Results.BadRequest(new { message = "Selecione um PDF." });
        var validation = AttachmentPolicy.Validate(file.FileName, file.Length, file.ContentType);
        if (validation.Error is not null || validation.Extension != ".pdf") return Results.BadRequest(new { message = "O arquivo deve ser um PDF válido." });
        await using var input = file.OpenReadStream();
        using var bytes = new MemoryStream();
        await input.CopyToAsync(bytes, ct);
        if (bytes.Length == 0 || bytes.Length < 5 || !bytes.ToArray().AsSpan(0, 5).SequenceEqual(Encoding.ASCII.GetBytes("%PDF-")))
            return Results.BadRequest(new { message = "O PDF está vazio ou corrompido." });
        byte[] content = bytes.ToArray();
        try
        {
            using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(content), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
            if (pdf.PageCount == 0) return Results.BadRequest(new { message = "O PDF não contém páginas." });
        }
        catch { return Results.BadRequest(new { message = "O arquivo não é um PDF válido." }); }
        var key = await storage.SaveEmployeeDocumentReplacementAsync(condominiumId, batch.Id, document.Id, content, ct);
        var previous = document.ReplaceFile(key, Convert.ToHexString(SHA256.HashData(content)), DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        if (previous != "pending") storage.Delete(previous);
        return Results.NoContent();
    }
}
