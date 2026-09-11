namespace CondoLink.Api.Features.EmployeeManagement;

public sealed record EmployeeResponse(
    Guid Id,
    Guid CondominiumId,
    string FullName,
    string? JobTitle,
    string? PhoneNumber,
    string? Email,
    string? RegistrationNumber,
    DateOnly? AdmissionDate,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record EmployeeRequest(
    string? FullName,
    string? JobTitle,
    string? PhoneNumber,
    string? Email,
    string? RegistrationNumber,
    DateOnly? AdmissionDate);

public sealed record EmployeeStatusRequest(bool IsActive);
