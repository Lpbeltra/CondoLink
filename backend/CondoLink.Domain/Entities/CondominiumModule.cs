using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class CondominiumModule
{
    private CondominiumModule() { }
    public CondominiumModule(Guid condominiumId, CondominiumModuleType module, bool isEnabled,
        bool managementCompanyAccessEnabled, DateTime now)
    {
        Id = Guid.NewGuid(); CondominiumId = condominiumId; Module = module;
        Set(isEnabled, managementCompanyAccessEnabled, now); CreatedAt = now;
    }
    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public CondominiumModuleType Module { get; private set; }
    public bool IsEnabled { get; private set; }
    public bool ManagementCompanyAccessEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public void Set(bool isEnabled, bool managementCompanyAccessEnabled, DateTime now)
    {
        IsEnabled = isEnabled;
        ManagementCompanyAccessEnabled = isEnabled && managementCompanyAccessEnabled;
        UpdatedAt = now;
    }
}
