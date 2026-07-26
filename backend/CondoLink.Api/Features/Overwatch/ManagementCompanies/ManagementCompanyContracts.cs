using CondoLink.Domain.Entities;

namespace CondoLink.Api.Features.Overwatch.ManagementCompanies;

public sealed record ManagementCompanyRequest(
    string? Name,
    string? LegalName,
    string? Document,
    string? Email,
    string? PhoneNumber);

public sealed record ManagementCompanyResponse(
    Guid Id,
    string Name,
    string? LegalName,
    string? Document,
    string? Email,
    string? PhoneNumber,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int CondominiumCount,
    int EmployeeCount)
{
    public static ManagementCompanyResponse From(
        ManagementCompany company,
        int condominiumCount = 0,
        int employeeCount = 0) =>
        new(
            company.Id,
            company.Name,
            company.LegalName,
            company.Document,
            company.Email,
            company.PhoneNumber,
            company.IsActive,
            company.CreatedAt,
            company.UpdatedAt,
            condominiumCount,
            employeeCount);
}

internal static class ManagementCompanyValidation
{
    public static string? Validate(ManagementCompanyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return "Name is required.";
        if (request.Name.Trim().Length > 150)
            return "Name must not exceed 150 characters.";
        if (request.LegalName?.Trim().Length > 200)
            return "Legal name must not exceed 200 characters.";
        if (request.Document?.Trim().Length > 20)
            return "Document must not exceed 20 characters.";
        if (request.Email?.Trim().Length > 254)
            return "Email must not exceed 254 characters.";
        if (request.PhoneNumber?.Trim().Length > 30)
            return "Phone number must not exceed 30 characters.";

        return null;
    }
}
