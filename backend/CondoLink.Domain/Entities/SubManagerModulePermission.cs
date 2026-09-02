using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class SubManagerModulePermission
{
    private SubManagerModulePermission() { }

    public SubManagerModulePermission(Guid membershipId, SubManagerModule module, Guid grantedByUserId)
    {
        if (membershipId == Guid.Empty || grantedByUserId == Guid.Empty) throw new ArgumentException("Permission context is required.");
        Id = Guid.NewGuid();
        CondominiumMembershipId = membershipId;
        Module = module;
        IsAllowed = true;
        GrantedByUserId = grantedByUserId;
        GrantedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid CondominiumMembershipId { get; private set; }
    public SubManagerModule Module { get; private set; }
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
