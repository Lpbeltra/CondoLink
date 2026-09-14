using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

/// <summary>
/// Explicit, company-wide module grant. This is intentionally separate from
/// the historical per-condominium permission rows so no old row can expand a
/// user's privilege during the Employee Management redesign.
/// </summary>
public sealed class ManagementCompanyEmployeeModuleGrant
{
    private ManagementCompanyEmployeeModuleGrant() { }

    public ManagementCompanyEmployeeModuleGrant(Guid managementCompanyEmployeeId,
        ManagementCompanyModuleType module, Guid grantedByUserId, DateTime now)
    {
        if (managementCompanyEmployeeId == Guid.Empty || grantedByUserId == Guid.Empty)
            throw new ArgumentException("Grant context is required.");
        Id = Guid.NewGuid();
        ManagementCompanyEmployeeId = managementCompanyEmployeeId;
        Module = module;
        IsAllowed = true;
        GrantedByUserId = grantedByUserId;
        GrantedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid ManagementCompanyEmployeeId { get; private set; }
    public ManagementCompanyModuleType Module { get; private set; }
    public bool IsAllowed { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }

    public void SetAllowed(bool allowed, Guid actorUserId, DateTime now)
    {
        IsAllowed = allowed;
        GrantedByUserId = actorUserId;
        if (allowed) { GrantedAt = now; RevokedAt = null; }
        else RevokedAt = now;
    }
}
