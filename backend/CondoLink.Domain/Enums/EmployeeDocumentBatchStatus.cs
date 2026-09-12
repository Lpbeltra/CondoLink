namespace CondoLink.Domain.Enums;

public enum EmployeeDocumentBatchStatus
{
    Uploaded = 1,
    Processing = 2,
    ReadyForReview = 3,
    Confirmed = 4,
    Distributing = 5,
    Completed = 6,
    Failed = 7
}
