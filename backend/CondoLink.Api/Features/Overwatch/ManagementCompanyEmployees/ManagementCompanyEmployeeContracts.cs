namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public sealed record ManagementCompanyEmployeeResponse(
    Guid Id,
    Guid ManagementCompanyId,
    Guid UserId,
    string FullName,
    string Email,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
