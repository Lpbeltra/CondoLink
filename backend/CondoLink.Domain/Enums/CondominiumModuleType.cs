namespace CondoLink.Domain.Enums;

/// <summary>Commercial capabilities owned by one condominium.</summary>
public enum CondominiumModuleType
{
    Assistant = 1,
    Documents = 2,
    Providers = 3,
    ManagementCompanyRequests = 4,
    // Historical persisted value. It is never consulted by Employee Management.
    EmployeeManagement = 5
}
