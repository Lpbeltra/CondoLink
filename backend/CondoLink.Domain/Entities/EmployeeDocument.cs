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

    public EmployeeDocument(Guid condominiumId, Guid batchId, EmployeeDocumentType documentType,
        int competenceMonth, int competenceYear, string fileKey, string originalFileName,
        int pageStart, int pageEnd, string contentHash, DateTime now)
    {
        if (condominiumId == Guid.Empty) throw new ArgumentException("Condominium id is required.", nameof(condominiumId));
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
    public Guid CondominiumId { get; private set; }
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
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }

    // The storage key is only known once the sliced PDF is written to storage,
    // which itself needs this document's Id — so it starts as a placeholder and
    // is assigned once, immediately after the file is persisted.
    public void SetFileKey(string fileKey)
    {
        if (FileKey != "pending") throw new InvalidOperationException("File key was already assigned.");
        if (string.IsNullOrWhiteSpace(fileKey)) throw new ArgumentException("File key is required.", nameof(fileKey));
        FileKey = fileKey;
    }

    public void ApplyAutomaticIdentification(Guid? employeeId,
        EmployeeDocumentIdentificationConfidence confidence,
        EmployeeDocumentIdentificationMethod method, DateTime now)
    {
        EmployeeId = employeeId;
        IdentificationConfidence = confidence;
        IdentificationMethod = method;
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
}
