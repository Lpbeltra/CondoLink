using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;

namespace CondoLink.Tests;

/// <summary>
/// Pure domain-level guards for the EmployeeDocument/EmployeeDocumentBatch state
/// machines — no HTTP, no database. These are the last line of defense if an
/// endpoint-level gate is ever bypassed or a future caller forgets to check
/// batch status first, so every "cannot happen after Confirmed" rule needs to
/// be enforced here too, not only at the API layer.
/// </summary>
public sealed class EmployeeDocumentStateMachineTests
{
    private static EmployeeDocument NewDocument() => new(Guid.NewGuid(), Guid.NewGuid(), EmployeeDocumentType.Payslip,
        8, 2026, "pending", "folha.pdf", 1, 1, "hash", DateTime.UtcNow);

    [Fact]
    public void Confirm_requires_an_associated_employee()
    {
        var document = NewDocument();
        var exception = Assert.Throws<InvalidOperationException>(() => document.Confirm(DateTime.UtcNow));
        Assert.Contains("cannot be confirmed", exception.Message);
    }

    [Fact]
    public void Confirmed_document_cannot_be_reassigned()
    {
        var document = NewDocument();
        var employeeId = Guid.NewGuid();
        document.AssignManually(employeeId, DateTime.UtcNow);
        document.Confirm(DateTime.UtcNow);
        var exception = Assert.Throws<InvalidOperationException>(() => document.AssignManually(Guid.NewGuid(), DateTime.UtcNow));
        Assert.Contains("cannot be changed", exception.Message);
        Assert.Equal(employeeId, document.EmployeeId); // unchanged
    }

    [Fact]
    public void Confirmed_document_cannot_be_cleared()
    {
        var document = NewDocument();
        document.AssignManually(Guid.NewGuid(), DateTime.UtcNow);
        document.Confirm(DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => document.ClearAssociation(DateTime.UtcNow));
        Assert.Equal(EmployeeDocumentIdentificationStatus.Confirmed, document.IdentificationStatus);
    }

    // Regression: Ignore() previously had no guard at all, unlike AssignManually
    // and ClearAssociation — a confirmed (and possibly already-distributed)
    // document's employee association could be silently wiped if this method
    // were ever reachable outside the one endpoint that currently gates on
    // batch status. The domain entity must enforce this invariant itself.
    [Fact]
    public void Confirmed_document_cannot_be_ignored()
    {
        var document = NewDocument();
        var employeeId = Guid.NewGuid();
        document.AssignManually(employeeId, DateTime.UtcNow);
        document.Confirm(DateTime.UtcNow);
        var exception = Assert.Throws<InvalidOperationException>(() => document.Ignore(DateTime.UtcNow));
        Assert.Contains("cannot be ignored", exception.Message);
        Assert.Equal(EmployeeDocumentIdentificationStatus.Confirmed, document.IdentificationStatus);
        Assert.Equal(employeeId, document.EmployeeId); // association survives the rejected call
    }

    [Fact]
    public void Unidentified_document_can_be_ignored()
    {
        var document = NewDocument();
        document.Ignore(DateTime.UtcNow);
        Assert.Equal(EmployeeDocumentIdentificationStatus.Ignored, document.IdentificationStatus);
        Assert.Null(document.EmployeeId);
    }

    [Fact]
    public void Batch_can_only_be_confirmed_from_ReadyForReview()
    {
        var batch = new EmployeeDocumentBatch(Guid.NewGuid(), EmployeeDocumentType.Payslip, 8, 2026, Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(EmployeeDocumentBatchStatus.Uploaded, batch.Status);
        Assert.Throws<InvalidOperationException>(() => batch.Confirm(Guid.NewGuid(), DateTime.UtcNow));

        batch.StartProcessing();
        Assert.Throws<InvalidOperationException>(() => batch.Confirm(Guid.NewGuid(), DateTime.UtcNow));

        batch.MarkReadyForReview();
        batch.Confirm(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(EmployeeDocumentBatchStatus.Confirmed, batch.Status);

        // Already confirmed — confirming again must not be silently accepted.
        Assert.Throws<InvalidOperationException>(() => batch.Confirm(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Failed_batch_cannot_be_confirmed()
    {
        var batch = new EmployeeDocumentBatch(Guid.NewGuid(), EmployeeDocumentType.Payslip, 8, 2026, Guid.NewGuid(), DateTime.UtcNow);
        batch.StartProcessing();
        batch.MarkFailed("boom");
        Assert.Equal(EmployeeDocumentBatchStatus.Failed, batch.Status);
        Assert.Throws<InvalidOperationException>(() => batch.Confirm(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void StartDistributing_is_a_no_op_from_states_other_than_Confirmed_or_Distributing()
    {
        var batch = new EmployeeDocumentBatch(Guid.NewGuid(), EmployeeDocumentType.Payslip, 8, 2026, Guid.NewGuid(), DateTime.UtcNow);
        batch.StartDistributing(); // Uploaded — must not silently jump to Distributing
        Assert.Equal(EmployeeDocumentBatchStatus.Uploaded, batch.Status);
    }
}
