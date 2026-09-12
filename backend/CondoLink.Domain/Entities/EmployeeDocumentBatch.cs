using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

/// <summary>
/// One document import operation (e.g. "Payslips — August/2026"): a set of
/// uploaded PDFs that get split into individual <see cref="EmployeeDocument"/>
/// rows, reviewed, and — only after explicit confirmation — distributed.
/// </summary>
public sealed class EmployeeDocumentBatch
{
    private EmployeeDocumentBatch() { }

    public EmployeeDocumentBatch(Guid condominiumId, EmployeeDocumentType documentType,
        int competenceMonth, int competenceYear, Guid createdByUserId, DateTime now)
    {
        if (condominiumId == Guid.Empty) throw new ArgumentException("Condominium id is required.", nameof(condominiumId));
        if (competenceMonth is < 1 or > 12) throw new ArgumentException("Competence month must be between 1 and 12.", nameof(competenceMonth));
        if (competenceYear is < 2000 or > 2100) throw new ArgumentException("Competence year is out of range.", nameof(competenceYear));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("Creator user id is required.", nameof(createdByUserId));

        Id = Guid.NewGuid();
        CondominiumId = condominiumId;
        DocumentType = documentType;
        CompetenceMonth = competenceMonth;
        CompetenceYear = competenceYear;
        Status = EmployeeDocumentBatchStatus.Uploaded;
        CreatedByUserId = createdByUserId;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public EmployeeDocumentType DocumentType { get; private set; }
    public int CompetenceMonth { get; private set; }
    public int CompetenceYear { get; private set; }
    public EmployeeDocumentBatchStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public Guid? ConfirmedByUserId { get; private set; }
    public string? FailureReason { get; private set; }

    // Raw uploaded PDFs awaiting split/identification, serialized as JSON. Only
    // read by the background processing worker; cleared once processing starts.
    public string? PendingUploadsJson { get; private set; }

    public void AttachPendingUploads(string pendingUploadsJson) => PendingUploadsJson = pendingUploadsJson;

    public void StartProcessing()
    {
        if (Status != EmployeeDocumentBatchStatus.Uploaded) return;
        Status = EmployeeDocumentBatchStatus.Processing;
    }

    // If the worker crashed/restarted mid-processing, nothing was ever persisted
    // (EmployeeDocument rows are only saved once, all together, at the very end
    // of a successful pass), so it is always safe to reset back to Uploaded and
    // let it be picked up and reprocessed from scratch — never stuck forever.
    public void RecoverInterruptedProcessing()
    {
        if (Status != EmployeeDocumentBatchStatus.Processing) return;
        Status = EmployeeDocumentBatchStatus.Uploaded;
    }

    public void MarkReadyForReview()
    {
        Status = EmployeeDocumentBatchStatus.ReadyForReview;
        PendingUploadsJson = null;
    }

    public void MarkFailed(string reason)
    {
        Status = EmployeeDocumentBatchStatus.Failed;
        FailureReason = reason.Length <= 500 ? reason : reason[..500];
        PendingUploadsJson = null;
    }

    public void Confirm(Guid actorUserId, DateTime now)
    {
        if (Status != EmployeeDocumentBatchStatus.ReadyForReview)
            throw new InvalidOperationException("Only a batch ready for review can be confirmed.");
        Status = EmployeeDocumentBatchStatus.Confirmed;
        ConfirmedAt = now;
        ConfirmedByUserId = actorUserId;
    }

    // Completed is included so a manual resend on an already-finished batch
    // (one failed delivery retried after the rest already went terminal) can
    // bring it back to Distributing instead of leaving it stuck showing
    // "Completed" while a fresh attempt is actually still in flight.
    public void StartDistributing()
    {
        if (Status is EmployeeDocumentBatchStatus.Confirmed or EmployeeDocumentBatchStatus.Distributing
            or EmployeeDocumentBatchStatus.Completed)
            Status = EmployeeDocumentBatchStatus.Distributing;
    }

    public void MarkCompleted()
    {
        if (Status != EmployeeDocumentBatchStatus.Distributing) return;
        Status = EmployeeDocumentBatchStatus.Completed;
    }
}
