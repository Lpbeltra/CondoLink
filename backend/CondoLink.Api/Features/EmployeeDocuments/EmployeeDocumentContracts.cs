using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.EmployeeDocuments;

public sealed record EmployeeDocumentBatchResponse(
    Guid Id, Guid CondominiumId, string DocumentType, int CompetenceMonth, int CompetenceYear,
    string Status, DateTime CreatedAt, string CreatedByName,
    DateTime? ConfirmedAt, string? ConfirmedByName, string? FailureReason,
    int DocumentCount, int IdentifiedCount, int NeedsReviewCount, int UnidentifiedCount,
    int IgnoredCount, int ConfirmedCount);

public sealed record EmployeeDocumentResponse(
    Guid Id, Guid BatchId, Guid? EmployeeId, string? EmployeeName,
    string DocumentType, int CompetenceMonth, int CompetenceYear,
    string OriginalFileName, int PageStart, int PageEnd,
    string IdentificationStatus, string IdentificationConfidence, string IdentificationMethod,
    DateTime CreatedAt, DateTime UpdatedAt, DateTime? ConfirmedAt,
    bool PossibleDuplicate);

/// <summary>Action: "Assign" (requires EmployeeId), "Clear", "Ignore" or "Confirm".</summary>
public sealed record UpdateEmployeeDocumentAssociationRequest(string Action, Guid? EmployeeId);

public sealed record EmployeeDocumentDistributionSummaryResponse(
    Guid BatchId, int TotalConfirmed, int Ready, int NoPhone, int InvalidPhone, int AlreadyQueuedOrSent);

public sealed record EmployeeDocumentDeliveryResponse(
    Guid EmployeeDocumentId, Guid EmployeeId, string EmployeeName,
    string Status, int AttemptCount, DateTime QueuedAt,
    DateTime? SentAt, DateTime? DeliveredAt, DateTime? ReadAt, DateTime? FailedAt,
    string? LastErrorCode, string? LastErrorDescription);

internal static class EmployeeDocumentTypeNames
{
    public static string ToDisplay(this EmployeeDocumentType type) => type.ToString();
}
