using CondoLink.Domain;
using CondoLink.Domain.Entities;

namespace CondoLink.Api.Features.Overwatch.Condominiums;

public sealed record CondominiumRequest(
    string? Name, string? Email, string? Cnpj, string? Address, string? City,
    string? State, bool HasDoorman, bool IsRemoteDoorman, string? DoormanContact);

public sealed record CondominiumResponse(
    Guid Id, string Name, string? Email, string? Cnpj, string? Address,
    string? City, string? State, bool HasDoorman, bool IsRemoteDoorman,
    string? DoormanContact, bool IsActive, DateTime CreatedAt, DateTime UpdatedAt,
    Guid? ManagementCompanyId, string? ManagementCompanyName, int ManagerCount);

internal static class CondominiumValidation
{
    public static string? Validate(CondominiumRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return "Name is required.";
        if (request.Name.Trim().Length > 200) return "Name must not exceed 200 characters.";
        if (request.Email?.Trim().Length > 254) return "Email must not exceed 254 characters.";
        if (!RegistrationData.IsValidCnpj(request.Cnpj)) return "CNPJ is invalid.";
        if (string.IsNullOrWhiteSpace(request.Address)) return "Address is required.";
        if (request.Address.Trim().Length > 200) return "Address must not exceed 200 characters.";
        if (string.IsNullOrWhiteSpace(request.City)) return "City is required.";
        if (request.City.Trim().Length > 100) return "City must not exceed 100 characters.";
        if (!RegistrationData.IsValidState(RegistrationData.State(request.State))) return "State is invalid.";
        if (request.DoormanContact?.Trim().Length > 100)
            return "Doorman contact must not exceed 100 characters.";
        return null;
    }
}
