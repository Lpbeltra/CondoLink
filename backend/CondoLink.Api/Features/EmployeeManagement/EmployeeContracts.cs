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
    DateTime UpdatedAt,
    string? Cpf = null);

public sealed record EmployeeRequest(
    string? FullName,
    string? JobTitle,
    string? PhoneNumber,
    string? Email,
    string? RegistrationNumber,
    DateOnly? AdmissionDate)
{
    public string? Cpf { get; init; }
    public Guid? CondominiumId { get; init; }
}

public sealed record EmployeeStatusRequest(bool IsActive);
