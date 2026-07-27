using CondoLink.Domain;
using CondoLink.Domain.Entities;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public sealed record ManagementCompanyRequest(
    string? Name, string? Cnpj, string? Address, string? City, string? State,
    string? Email, string? PhoneNumber);

public sealed record ManagementCompanyResponse(
    Guid Id, string Name, string? Cnpj, string? Address, string? City,
    string? State, string? Email, string? PhoneNumber, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt, int CondominiumCount, int EmployeeCount)
{
    public static ManagementCompanyResponse From(
        ManagementCompany company, int condominiumCount = 0, int employeeCount = 0) =>
        new(company.Id, company.Name, company.Cnpj, company.Address, company.City,
            company.State, company.Email, company.PhoneNumber, company.IsActive,
            company.CreatedAt, company.UpdatedAt, condominiumCount, employeeCount);
}

internal static class ManagementCompanyValidation
{
    public static string? Validate(ManagementCompanyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (request.Name.Trim().Length > 150) return "Name must not exceed 150 characters.";
        if (!RegistrationData.IsValidCnpj(request.Cnpj)) return "CNPJ is invalid.";
        if (string.IsNullOrWhiteSpace(request.Address)) return "Address is required.";
        if (request.Address.Trim().Length > 200) return "Address must not exceed 200 characters.";
        if (string.IsNullOrWhiteSpace(request.City)) return "City is required.";
        if (request.City.Trim().Length > 100) return "City must not exceed 100 characters.";
        if (!RegistrationData.IsValidState(RegistrationData.State(request.State))) return "State is invalid.";
        if (request.Email?.Trim().Length > 254) return "Email must not exceed 254 characters.";
        if (request.PhoneNumber?.Trim().Length > 30) return "Phone number must not exceed 30 characters.";
        return null;
    }
}
