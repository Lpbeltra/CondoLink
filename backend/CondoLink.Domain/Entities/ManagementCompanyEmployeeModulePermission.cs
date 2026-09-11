using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

/// <summary>
/// Grants a management company employee (or department) access to a specific
/// delegable <see cref="CondominiumModuleType"/>, independent of any condominium.
/// Delegation for a particular condominium still requires
/// <see cref="CondominiumModule.ManagementCompanyAccessEnabled"/> and a current
/// company link; this row only answers "is this employee allowed to use the module at all".
/// </summary>
public sealed class ManagementCompanyEmployeeModulePermission
{
    private ManagementCompanyEmployeeModulePermission() { }

    public ManagementCompanyEmployeeModulePermission(Guid managementCompanyEmployeeId, CondominiumModuleType module, Guid grantedByUserId)
    {
        if (managementCompanyEmployeeId == Guid.Empty || grantedByUserId == Guid.Empty)
            throw new ArgumentException("Permission context is required.");
        Id = Guid.NewGuid();
        ManagementCompanyEmployeeId = managementCompanyEmployeeId;
        Module = module;
        IsAllowed = true;
        GrantedByUserId = grantedByUserId;
        GrantedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ManagementCompanyEmployeeId { get; private set; }
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
