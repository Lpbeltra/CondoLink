using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class ManagementCompanyModule
{
    private ManagementCompanyModule() { }
    public ManagementCompanyModule(Guid managementCompanyId, ManagementCompanyModuleType module, bool isEnabled, DateTime now)
    {
        if (managementCompanyId == Guid.Empty) throw new ArgumentException("Management company id is required.", nameof(managementCompanyId));
        Id = Guid.NewGuid(); ManagementCompanyId = managementCompanyId; Module = module; IsEnabled = isEnabled; UpdatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid ManagementCompanyId { get; private set; }
    public ManagementCompanyModuleType Module { get; private set; }
    public bool IsEnabled { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public void SetEnabled(bool enabled, DateTime now) { IsEnabled = enabled; UpdatedAt = now; }
}
