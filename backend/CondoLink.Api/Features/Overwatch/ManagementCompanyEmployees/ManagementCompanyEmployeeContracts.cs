using CondoLink.Domain.Enums;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanyEmployees;

public sealed record ManagementCompanyEmployeeResponse(
    Guid Id,
    Guid ManagementCompanyId,
    Guid UserId,
    string FullName,
    string Email,
    string? Contact,
    string JobTitle,
    ManagementCompanyAccessType AccessType,
    bool IsActive,
    DateTime? LastAccessAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<Guid> CategoryIds);
