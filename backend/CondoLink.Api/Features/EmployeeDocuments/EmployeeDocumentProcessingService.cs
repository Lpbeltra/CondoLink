using System.Security.Cryptography;
using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.EmployeeDocuments;

public sealed record PendingEmployeeDocumentUpload(string StorageKey, string OriginalFileName);

/// <summary>
/// Turns a batch's raw uploaded PDFs into reviewable <see cref="EmployeeDocument"/>
/// rows: native text extraction, OCR fallback for scanned pages (reusing the
/// same infrastructure the condominium Documents/Assistant feature uses — never
/// a parallel OCR integration, and never routed into embeddings/RAG), document
/// splitting, and deterministic employee identification. Runs off the HTTP
/// request thread, invoked by <see cref="EmployeeDocumentProcessingWorker"/>.
/// </summary>
public sealed class EmployeeDocumentProcessingService(
    AppDbContext db, LocalFileStorage storage, CondominiumDocumentProcessor ocrProcessor,
    ILogger<EmployeeDocumentProcessingService> logger)
{
    public async Task ProcessAsync(EmployeeDocumentBatch batch, CancellationToken ct)
    {
        batch.StartProcessing();
        await db.SaveChangesAsync(ct);
        try
        {
            var uploads = JsonSerializer.Deserialize<PendingEmployeeDocumentUpload[]>(batch.PendingUploadsJson ?? "[]") ?? [];
            if (uploads.Length == 0) throw new InvalidOperationException("Batch has no pending uploads.");

            var candidates = await db.Employees.AsNoTracking()
                .Where(x => x.CondominiumId == batch.CondominiumId)
                .Select(x => new EmployeeDocumentSplitter.Candidate(x.Id, x.FullName, x.NormalizedRegistrationNumber, x.JobTitle))
                .ToArrayAsync(ct);

            var created = new List<EmployeeDocument>();
            foreach (var upload in uploads)
            {
                using var sourceStream = storage.OpenRead(upload.StorageKey)
                    ?? throw new InvalidOperationException("Uploaded file could not be found in storage.");
                var pages = CondominiumDocumentText.ExtractPages(sourceStream, ".pdf");
                var withOcr = await ocrProcessor.OcrMissingPagesAsync(pages, ct);
                if (withOcr.Sum(page => page.Text.Length) < 20)
                    throw new InvalidOperationException(
                        $"Não foi possível extrair texto de \"{upload.OriginalFileName}\". O arquivo pode estar corrompido ou ser uma imagem sem texto reconhecível.");

                var segments = EmployeeDocumentSplitter.Segment(
                    withOcr.Select(page => (page.PageNumber!.Value, page.Text)).ToArray(), candidates);

                foreach (var segment in segments)
                {
                    var slicedBytes = EmployeeDocumentPdfSlicer.Slice(RewindCopy(sourceStream), segment.PageStart, segment.PageEnd);
                    // Hash the extracted TEXT of the segment, not the re-sliced PDF
                    // bytes: PdfSharp embeds a fresh creation timestamp/document id on
                    // every Save(), so re-slicing byte-identical source content twice
                    // produces two DIFFERENT files — which would silently defeat
                    // duplicate detection for the most common real case (the exact
                    // same payslip re-imported). The same problem shows up with real
                    // payroll exports that stamp their own generation timestamp into
                    // the PDF metadata on every run. Text content is what actually
                    // identifies "the same document" here, and extraction is
                    // deterministic for the same input bytes.
                    var segmentText = string.Join("\n", withOcr
                        .Where(page => page.PageNumber >= segment.PageStart && page.PageNumber <= segment.PageEnd)
                        .OrderBy(page => page.PageNumber)
                        .Select(page => page.Text));
                    var contentHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(segmentText)));
                    var document = new EmployeeDocument(batch.CondominiumId, batch.Id, batch.DocumentType,
                        batch.CompetenceMonth, batch.CompetenceYear, "pending", upload.OriginalFileName,
                        segment.PageStart, segment.PageEnd, contentHash, DateTime.UtcNow);
                    var fileKey = await storage.SaveEmployeeDocumentAsync(batch.CondominiumId, batch.Id, document.Id, slicedBytes, ct);
                    document.SetFileKey(fileKey);
                    document.ApplyAutomaticIdentification(segment.EmployeeId, segment.Confidence, segment.Method, DateTime.UtcNow);
                    created.Add(document);
                }
            }

            db.EmployeeDocuments.AddRange(created);
            batch.MarkReadyForReview();
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Employee document batch processed. CondominiumId: {CondominiumId}; BatchId: {BatchId}; Documents: {DocumentCount}.",
                batch.CondominiumId, batch.Id, created.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            foreach (var entry in db.ChangeTracker.Entries<EmployeeDocument>()
                .Where(entry => entry.Entity.BatchId == batch.Id && entry.State == EntityState.Added))
                entry.State = EntityState.Detached;
            batch.MarkFailed(exception.Message.Length <= 500 ? exception.Message : "Falha ao processar os documentos enviados.");
            await db.SaveChangesAsync(ct);
            logger.LogWarning(exception,
                "Employee document batch processing failed. CondominiumId: {CondominiumId}; BatchId: {BatchId}.",
                batch.CondominiumId, batch.Id);
        }
    }

    // EmployeeDocumentPdfSlicer positions the stream itself, but PdfPig's PdfDocument
    // already consumed/positioned the same stream during extraction; give the slicer
    // its own rewindable view rather than depend on extraction leaving it seekable.
    private static Stream RewindCopy(Stream source)
    {
        source.Position = 0;
        return source;
    }
}
