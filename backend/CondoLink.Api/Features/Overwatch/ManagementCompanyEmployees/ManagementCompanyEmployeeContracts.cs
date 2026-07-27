namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public sealed record ManagementCompanyEmployeeResponse(
    Guid Id,
    Guid ManagementCompanyId,
    Guid UserId,
    string FullName,
    string Email,
    string? Contact,
    string JobTitle,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);
