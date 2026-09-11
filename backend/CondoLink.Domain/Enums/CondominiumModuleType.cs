namespace CondoLink.Domain.Enums;

/// <summary>Commercial capabilities owned by one condominium.</summary>
public enum CondominiumModuleType
{
    Assistant = 1,
    Documents = 2,
    Providers = 3,
    ManagementCompanyRequests = 4,
    // Reserved: feature is intentionally not implemented in this release.
    EmployeeManagement = 5
}
