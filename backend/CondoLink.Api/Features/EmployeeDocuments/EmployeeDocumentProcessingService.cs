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
            batch.SetProgress("Extração", 0, uploads.Length);
            await db.SaveChangesAsync(ct);

            // No pre-selection: the candidate universe is every employee currently
            // within a condominium this management company administers — the same
            // authorization boundary used everywhere else (AdministratorEmployeeEndpoints,
            // manual assign). Which of them a document actually belongs to is decided
            // entirely by the PDF's own content (CPF/CNPJ/registration/name), not by
            // anything chosen ahead of time on the employee list screen.
            var candidates = await db.Employees.AsNoTracking()
                .Where(x => db.Condominiums.Any(c => c.Id == x.CondominiumId && c.ManagementCompanyId == batch.ManagementCompanyId && c.IsActive))
                .Select(x => new { x.Id, x.CondominiumId, x.FullName, x.NormalizedRegistrationNumber, x.JobTitle, x.NormalizedCpf,
                    Cnpj = db.Condominiums.Where(c => c.Id == x.CondominiumId).Select(c => c.Cnpj).FirstOrDefault() })
                .ToArrayAsync(ct);
            var candidateCondominiums = candidates.ToDictionary(x => x.Id, x => x.CondominiumId);
            var splitterCandidates = candidates.Select(x => new EmployeeDocumentSplitter.Candidate(x.Id, x.FullName, x.NormalizedRegistrationNumber, x.JobTitle, x.NormalizedCpf, x.Cnpj)).ToArray();

            var created = new List<EmployeeDocument>();
            foreach (var upload in uploads)
            {
                batch.SetProgress("Extração/OCR", batch.ProcessedItems, uploads.Length);
                using var sourceStream = storage.OpenRead(upload.StorageKey)
                    ?? throw new InvalidOperationException("Uploaded file could not be found in storage.");
                using var sourceBytes = new MemoryStream();
                await sourceStream.CopyToAsync(sourceBytes, ct);
                var pages = CondominiumDocumentText.ExtractPages(new MemoryStream(sourceBytes.ToArray()), ".pdf");
                var withOcr = await ocrProcessor.OcrMissingPagesAsync(pages, ct);
                if (withOcr.Sum(page => page.Text.Length) < 20)
                    throw new InvalidOperationException(
                        $"Não foi possível extrair texto de \"{upload.OriginalFileName}\". O arquivo pode estar corrompido ou ser uma imagem sem texto reconhecível.");

                var segments = EmployeeDocumentSplitter.Segment(
                    withOcr.Select(page => (page.PageNumber!.Value, page.Text)).ToArray(), splitterCandidates);

                foreach (var segment in segments)
                {
                    var slicedBytes = EmployeeDocumentPdfSlicer.Slice(new MemoryStream(sourceBytes.ToArray()), segment.PageStart, segment.PageEnd);
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
                    Guid? documentCondominiumId = segment.EmployeeId is Guid employeeId && candidateCondominiums.TryGetValue(employeeId, out var employeeCondominiumId)
                        ? employeeCondominiumId : null;
                    var document = new EmployeeDocument(documentCondominiumId, batch.Id, batch.DocumentType,
                        batch.CompetenceMonth, batch.CompetenceYear, "pending", upload.OriginalFileName,
                        segment.PageStart, segment.PageEnd, contentHash, DateTime.UtcNow);
                    var fileKey = await storage.SaveEmployeeDocumentAsync(documentCondominiumId ?? batch.ManagementCompanyId!.Value, batch.Id, document.Id, slicedBytes, ct);
                    document.SetFileKey(fileKey);
                    document.ApplyAutomaticIdentification(segment.EmployeeId, segment.Confidence, segment.Method, DateTime.UtcNow,
                        segment.ExtractedCpfDigits, segment.ExtractedCnpjDigits);
                    created.Add(document);
                }
                batch.SetProgress("Separação/Identificação", batch.ProcessedItems + 1, uploads.Length);
                await db.SaveChangesAsync(ct);
            }

            // EmployeeDocumentBatchEmployee is no longer pre-populated from a manual
            // selection; it now records — as an auditable scope/authorization anchor —
            // exactly which employees the automatic matching actually identified.
            // Employees assigned later during manual review are added at that point too
            // (see UpdateEmployeeDocumentAssociation).
            var identifiedEmployeeIds = created.Where(d => d.EmployeeId.HasValue).Select(d => d.EmployeeId!.Value).Distinct().ToArray();
            if (identifiedEmployeeIds.Length > 0)
                db.EmployeeDocumentBatchEmployees.AddRange(identifiedEmployeeIds.Select(id => new EmployeeDocumentBatchEmployee(batch.Id, id)));

            db.EmployeeDocuments.AddRange(created);
            batch.MarkReadyForReview();
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Employee document batch processed. ManagementCompanyId: {ManagementCompanyId}; BatchId: {BatchId}; Documents: {DocumentCount}.",
                batch.ManagementCompanyId, batch.Id, created.Count);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            foreach (var entry in db.ChangeTracker.Entries<EmployeeDocument>()
                .Where(entry => entry.Entity.BatchId == batch.Id && entry.State == EntityState.Added))
                entry.State = EntityState.Detached;
            batch.MarkFailed(exception.Message.Length <= 500 ? exception.Message : "Falha ao processar os documentos enviados.");
            await db.SaveChangesAsync(ct);
            logger.LogWarning(exception,
                "Employee document batch processing failed. ManagementCompanyId: {ManagementCompanyId}; BatchId: {BatchId}.",
                batch.ManagementCompanyId, batch.Id);
        }
    }

}
