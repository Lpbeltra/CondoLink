namespace CondoLink.Api.Features.Overwatch.Managers;

public sealed record OverwatchManagerResponse(
    Guid Id,
    string FullName,
    string Email,
    bool IsActive,
    int CondominiumCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ManagerCondominiumResponse(
    Guid MembershipId,
    Guid CondominiumId,
    string Name,
    string? ManagementCompanyName,
    bool IsActive,
    DateTime JoinedAt);
