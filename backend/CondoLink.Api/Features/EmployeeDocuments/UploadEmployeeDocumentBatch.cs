using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public static class UploadEmployeeDocumentBatch
{
    public const int MaximumFileCount = 20;
    public const long MaximumRequestSize = 200 * 1024 * 1024;
    private static readonly byte[] PdfMagicBytes = Encoding.ASCII.GetBytes("%PDF-");

    public static IEndpointRouteBuilder MapUploadEmployeeDocumentBatch(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/condominiums/{condominiumId:guid}/employees/documents/batches", HandleAsync)
            .RequireAuthorization()
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaximumRequestSize))
            .WithTags("EmployeeDocuments")
            .WithSummary("Upload payslip PDFs to be split, identified and reviewed");
        return endpoints;
    }

    private static async Task<IResult> HandleAsync(
        Guid condominiumId, HttpRequest request, System.Security.Claims.ClaimsPrincipal principal,
        AppDbContext db, EmployeeManagement.EmployeeManagementAccessService access,
        LocalFileStorage storage, CancellationToken ct)
    {
        var actor = await access.RequireAsync(principal, condominiumId, ct);

        if (!request.HasFormContentType)
            return Results.BadRequest(new { message = "Envie os arquivos usando multipart/form-data." });
        IFormCollection form;
        try { form = await request.ReadFormAsync(ct); }
        catch (InvalidDataException) { return Results.BadRequest(new { message = "Não foi possível ler os arquivos enviados." }); }

        if (!int.TryParse(form["competenceMonth"], out var month) || month is < 1 or > 12)
            return Results.BadRequest(new { message = "Informe o mês de competência (1-12)." });
        if (!int.TryParse(form["competenceYear"], out var year) || year is < 2000 or > 2100)
            return Results.BadRequest(new { message = "Informe o ano de competência." });
        var documentType = EmployeeDocumentType.Payslip;

        var files = form.Files.GetFiles("files");
        if (files.Count == 0) return Results.BadRequest(new { message = "Selecione ao menos um arquivo PDF." });
        if (files.Count > MaximumFileCount)
            return Results.BadRequest(new { message = $"É permitido enviar no máximo {MaximumFileCount} arquivos por vez." });

        var validatedContents = new List<(string OriginalFileName, byte[] Content)>();
        foreach (var file in files)
        {
            var result = AttachmentPolicy.Validate(file.FileName, file.Length, file.ContentType);
            if (result.Error is not null || result.Extension != ".pdf")
                return Results.BadRequest(new { message = $"\"{file.FileName}\" deve ser um PDF válido." });

            await using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, ct);
            var content = buffer.ToArray();
            if (content.Length == 0 || content.Length < PdfMagicBytes.Length
                || !content.AsSpan(0, PdfMagicBytes.Length).SequenceEqual(PdfMagicBytes))
                return Results.BadRequest(new { message = $"\"{file.FileName}\" está vazio ou corrompido." });

            validatedContents.Add((result.Name!, content));
        }

        var batch = new EmployeeDocumentBatch(condominiumId, documentType, month, year, actor.UserId, DateTime.UtcNow);
        var savedKeys = new List<string>();
        try
        {
            var pendingUploads = new List<PendingEmployeeDocumentUpload>();
            foreach (var item in validatedContents)
            {
                using var contentStream = new MemoryStream(item.Content);
                var key = await storage.SaveEmployeeDocumentBatchFileAsync(condominiumId, batch.Id, contentStream, ".pdf", ct);
                savedKeys.Add(key);
                pendingUploads.Add(new PendingEmployeeDocumentUpload(key, item.OriginalFileName));
            }
            batch.AttachPendingUploads(JsonSerializer.Serialize(pendingUploads));
            db.EmployeeDocumentBatches.Add(batch);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            foreach (var key in savedKeys) storage.Delete(key);
            throw;
        }

        return Results.Accepted($"/condominiums/{condominiumId}/employees/documents/batches/{batch.Id}",
            new { batch.Id, Status = batch.Status.ToString() });
    }
}
