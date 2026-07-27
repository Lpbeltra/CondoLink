namespace CondoLink.Api.Features.Overwatch.Managers;

public sealed record OverwatchManagerResponse(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    string? Cpf,
    string? Cnpj,
    string? Address,
    string? City,
    string? State,
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

public sealed record CondominiumManagerResponse(
    Guid MembershipId,
    Guid UserId,
    string FullName,
    string Email,
    string? PhoneNumber,
    bool IsActive,
    DateTime JoinedAt);
