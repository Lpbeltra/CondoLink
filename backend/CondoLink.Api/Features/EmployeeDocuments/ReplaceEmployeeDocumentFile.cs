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
        endpoints.MapPut("/administrator/employees/documents/batches/{batchId:guid}/documents/{documentId:guid}/file", HandleAsync).RequireAuthorization().DisableAntiforgery().WithTags("EmployeeDocuments");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(Guid batchId, Guid documentId, HttpRequest request, ClaimsPrincipal principal,
        AppDbContext db, EmployeeManagement.EmployeeManagementAccessService access, LocalFileStorage storage, CancellationToken ct)
    {
        var (_, batch) = await access.RequireBatchAsync(principal, batchId, ct);
        var document = await db.EmployeeDocuments.SingleOrDefaultAsync(x => x.Id == documentId && x.BatchId == batchId, ct);
        if (document is null) return Results.NotFound(new { message = "Documento não encontrado." });
        if (batch.Status != EmployeeDocumentBatchStatus.ReadyForReview) return Results.Conflict(new { message = "O arquivo não pode ser substituído depois da confirmação." });
        if (!request.HasFormContentType) return Results.BadRequest(new { message = "Envie um PDF." });
        var file = (await request.ReadFormAsync(ct)).Files.GetFile("file");
        if (file is null) return Results.BadRequest(new { message = "Selecione um PDF." });
        var validation = AttachmentPolicy.Validate(file.FileName, file.Length, file.ContentType);
        if (validation.Error is not null || validation.Extension != ".pdf") return Results.BadRequest(new { message = "O arquivo deve ser um PDF válido." });
        await using var input = file.OpenReadStream(); using var bytes = new MemoryStream(); await input.CopyToAsync(bytes, ct);
        var content = bytes.ToArray();
        if (content.Length < 5 || !content.AsSpan(0, 5).SequenceEqual(Encoding.ASCII.GetBytes("%PDF-"))) return Results.BadRequest(new { message = "O PDF está vazio ou corrompido." });
        try { using var pdf = PdfSharp.Pdf.IO.PdfReader.Open(new MemoryStream(content), PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import); if (pdf.PageCount == 0) return Results.BadRequest(new { message = "O PDF não contém páginas." }); }
        catch { return Results.BadRequest(new { message = "O arquivo não é um PDF válido." }); }
        var key = await storage.SaveEmployeeDocumentReplacementAsync(document.CondominiumId ?? batch.ManagementCompanyId!.Value, batch.Id, document.Id, content, ct);
        var previous = document.ReplaceFile(key, Convert.ToHexString(SHA256.HashData(content)), DateTime.UtcNow);
        await db.SaveChangesAsync(ct); if (previous != "pending") storage.Delete(previous);
        return Results.NoContent();
    }
}
