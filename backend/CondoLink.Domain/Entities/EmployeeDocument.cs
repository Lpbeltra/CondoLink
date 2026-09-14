using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

/// <summary>
/// One individualized document (e.g. a single employee's payslip page range)
/// split out of a batch upload. Always scoped to a single condominium; never
/// indexed for the Assistant/RAG and never exposed through generic document
/// endpoints — this is private employee data with its own access surface.
/// </summary>
public sealed class EmployeeDocument
{
    private EmployeeDocument() { }

    public EmployeeDocument(Guid? condominiumId, Guid batchId, EmployeeDocumentType documentType,
        int competenceMonth, int competenceYear, string fileKey, string originalFileName,
        int pageStart, int pageEnd, string contentHash, DateTime now)
    {
        if (batchId == Guid.Empty) throw new ArgumentException("Batch id is required.", nameof(batchId));
        if (string.IsNullOrWhiteSpace(fileKey)) throw new ArgumentException("File key is required.", nameof(fileKey));
        if (pageStart < 1 || pageEnd < pageStart) throw new ArgumentException("Page range is invalid.", nameof(pageEnd));
        if (string.IsNullOrWhiteSpace(contentHash)) throw new ArgumentException("Content hash is required.", nameof(contentHash));

        Id = Guid.NewGuid();
        CondominiumId = condominiumId;
        BatchId = batchId;
        DocumentType = documentType;
        CompetenceMonth = competenceMonth;
        CompetenceYear = competenceYear;
        FileKey = fileKey;
        OriginalFileName = originalFileName.Length <= 260 ? originalFileName : originalFileName[..260];
        PageStart = pageStart;
        PageEnd = pageEnd;
        ContentHash = contentHash;
        IdentificationStatus = EmployeeDocumentIdentificationStatus.Unidentified;
        IdentificationConfidence = EmployeeDocumentIdentificationConfidence.None;
        IdentificationMethod = EmployeeDocumentIdentificationMethod.None;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid? CondominiumId { get; private set; }
    public Guid BatchId { get; private set; }
    public Guid? EmployeeId { get; private set; }
    public EmployeeDocumentType DocumentType { get; private set; }
    public int CompetenceMonth { get; private set; }
    public int CompetenceYear { get; private set; }
    public string FileKey { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public int PageStart { get; private set; }
    public int PageEnd { get; private set; }
    public string ContentHash { get; private set; } = null!;
    public EmployeeDocumentIdentificationStatus IdentificationStatus { get; private set; }
    public EmployeeDocumentIdentificationConfidence IdentificationConfidence { get; private set; }
    public EmployeeDocumentIdentificationMethod IdentificationMethod { get; private set; }
    // Digits-only CPF/CNPJ actually found in this document's text at the time it was
    // split/matched — independent of whether they resolved to an employee. Kept so
    // manual review can explain a mismatch and so send-time revalidation can catch
    // drift (e.g. the employee's CPF was corrected, or moved condominiums) even
    // though the FK association itself still looks internally consistent.
    public string? ExtractedCpfDigits { get; private set; }
    public string? ExtractedCnpjDigits { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    // Soft-delete: removes the document from normal operational flow while
    // keeping the row (and every EmployeeDocumentDelivery/WhatsAppOutboundMessage
    // referencing it) intact for audit/payroll-history purposes.
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }

    // The storage key is only known once the sliced PDF is written to storage,
    // which itself needs this document's Id — so it starts as a placeholder and
    // is assigned once, immediately after the file is persisted.
    public void SetFileKey(string fileKey)
    {
        if (FileKey != "pending") throw new InvalidOperationException("File key was already assigned.");
        if (string.IsNullOrWhiteSpace(fileKey)) throw new ArgumentException("File key is required.", nameof(fileKey));
        FileKey = fileKey;
    }

    public string ReplaceFile(string fileKey, string contentHash, DateTime now)
    {
        if (IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed)
            throw new InvalidOperationException("A confirmed document's file cannot be replaced.");
        if (string.IsNullOrWhiteSpace(fileKey) || string.IsNullOrWhiteSpace(contentHash))
            throw new ArgumentException("Replacement file metadata is required.");
        var previous = FileKey;
        FileKey = fileKey;
        ContentHash = contentHash;
        IdentificationStatus = EmployeeDocumentIdentificationStatus.NeedsReview;
        IdentificationConfidence = EmployeeDocumentIdentificationConfidence.None;
        IdentificationMethod = EmployeeDocumentIdentificationMethod.None;
        // The old extracted CPF/CNPJ described the REPLACED file's content; keeping
        // them would let a stale hard gate block (or wrongly allow) a manual
        // association against the new file's actual content.
        ExtractedCpfDigits = null;
        ExtractedCnpjDigits = null;
        ConfirmedAt = null;
        UpdatedAt = now;
        return previous;
    }

    public void ApplyAutomaticIdentification(Guid? employeeId,
        EmployeeDocumentIdentificationConfidence confidence,
        EmployeeDocumentIdentificationMethod method, DateTime now,
        string? extractedCpfDigits = null, string? extractedCnpjDigits = null)
    {
        EmployeeId = employeeId;
        IdentificationConfidence = confidence;
        IdentificationMethod = method;
        ExtractedCpfDigits = extractedCpfDigits;
        ExtractedCnpjDigits = extractedCnpjDigits;
        IdentificationStatus = employeeId is null
            ? EmployeeDocumentIdentificationStatus.Unidentified
            : confidence == EmployeeDocumentIdentificationConfidence.High
                ? EmployeeDocumentIdentificationStatus.Identified
                : EmployeeDocumentIdentificationStatus.NeedsReview;
        UpdatedAt = now;
    }

    public void AssignManually(Guid employeeId, DateTime now)
    {
        if (IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed)
            throw new InvalidOperationException("A confirmed document's association cannot be changed; clear it first.");
        EmployeeId = employeeId;
        IdentificationMethod = EmployeeDocumentIdentificationMethod.Manual;
        IdentificationConfidence = EmployeeDocumentIdentificationConfidence.High;
        IdentificationStatus = EmployeeDocumentIdentificationStatus.NeedsReview;
        UpdatedAt = now;
    }

    public void SetCondominium(Guid condominiumId)
    {
        if (condominiumId == Guid.Empty) throw new ArgumentException("Condominium id is required.", nameof(condominiumId));
        CondominiumId = condominiumId;
    }

    public void ClearAssociation(DateTime now)
    {
        if (IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed)
            throw new InvalidOperationException("A confirmed document's association cannot be cleared directly.");
        EmployeeId = null;
        IdentificationMethod = EmployeeDocumentIdentificationMethod.None;
        IdentificationConfidence = EmployeeDocumentIdentificationConfidence.None;
        IdentificationStatus = EmployeeDocumentIdentificationStatus.Unidentified;
        UpdatedAt = now;
    }

    public void Confirm(DateTime now)
    {
        if (EmployeeId is null)
            throw new InvalidOperationException("A document without an associated employee cannot be confirmed.");
        IdentificationStatus = EmployeeDocumentIdentificationStatus.Confirmed;
        ConfirmedAt = now;
        UpdatedAt = now;
    }

    public void Ignore(DateTime now)
    {
        if (IdentificationStatus == EmployeeDocumentIdentificationStatus.Confirmed)
            throw new InvalidOperationException("A confirmed document cannot be ignored; clear it first.");
        EmployeeId = null;
        IdentificationStatus = EmployeeDocumentIdentificationStatus.Ignored;
        ConfirmedAt = now;
        UpdatedAt = now;
    }

    // Only a document already past active review (Confirmed or Ignored) can be
    // soft-deleted — a document still Unidentified/NeedsReview belongs to the
    // normal review flow (Assign/Ignore), not deletion. Delivery/outbound-status
    // checks (an active send in flight) live at the endpoint, which has that data.
    public void SoftDelete(Guid actorUserId, DateTime now)
    {
        if (DeletedAt is not null)
            throw new InvalidOperationException("Document was already deleted.");
        if (IdentificationStatus is not (EmployeeDocumentIdentificationStatus.Confirmed or EmployeeDocumentIdentificationStatus.Ignored))
            throw new InvalidOperationException("Only a confirmed or ignored document can be deleted.");
        DeletedAt = now;
        DeletedByUserId = actorUserId;
        UpdatedAt = now;
    }
}
