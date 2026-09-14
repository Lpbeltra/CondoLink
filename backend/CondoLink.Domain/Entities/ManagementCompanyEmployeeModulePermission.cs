using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

/// <summary>
/// Grants a management company employee access to a module in one condominium.
/// Legacy rows without CondominiumId are retained for audit but never authorize access.
/// </summary>
public sealed class ManagementCompanyEmployeeModulePermission
{
    private ManagementCompanyEmployeeModulePermission() { }

    public ManagementCompanyEmployeeModulePermission(Guid managementCompanyEmployeeId, Guid condominiumId, CondominiumModuleType module, Guid grantedByUserId)
        : this(managementCompanyEmployeeId, (Guid?)condominiumId, module, grantedByUserId) { }

    public ManagementCompanyEmployeeModulePermission(Guid managementCompanyEmployeeId, CondominiumModuleType module, Guid grantedByUserId)
        : this(managementCompanyEmployeeId, null, module, grantedByUserId) { }

    private ManagementCompanyEmployeeModulePermission(Guid managementCompanyEmployeeId, Guid? condominiumId, CondominiumModuleType module, Guid grantedByUserId)
    {
        if (managementCompanyEmployeeId == Guid.Empty || grantedByUserId == Guid.Empty)
            throw new ArgumentException("Permission context is required.");
        Id = Guid.NewGuid();
        ManagementCompanyEmployeeId = managementCompanyEmployeeId;
        CondominiumId = condominiumId;
        Module = module;
        IsAllowed = true;
        GrantedByUserId = grantedByUserId;
        GrantedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ManagementCompanyEmployeeId { get; private set; }
    public Guid? CondominiumId { get; private set; }
    public CondominiumModuleType Module { get; private set; }
    public bool IsAllowed { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public void SetAllowed(bool allowed, Guid actorUserId)
    {
        IsAllowed = allowed;
        GrantedByUserId = actorUserId;
        if (allowed) { GrantedAt = DateTime.UtcNow; RevokedAt = null; }
        else RevokedAt = DateTime.UtcNow;
    }
}
