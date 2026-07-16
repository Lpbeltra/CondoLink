using CondoLink.Domain.Common;
using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public class CondominiumMembershipRole : Entity
{
    protected CondominiumMembershipRole()
    {
    }

    internal CondominiumMembershipRole(
        CondominiumMembership condominiumMembership,
        CondominiumRole role)
    {
        var membership = Guard.AgainstNull(
            condominiumMembership,
            nameof(condominiumMembership));

        CondominiumMembershipId = membership.Id;
        Role = Guard.AgainstInvalidEnum(role, nameof(role));
        IsActive = true;
        GrantedAt = DateTimeOffset.UtcNow;
    }

    public Guid CondominiumMembershipId { get; private set; }

    public CondominiumRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    internal void Revoke(DateTimeOffset revokedAt)
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        RevokedAt = revokedAt;
    }

    internal void Reactivate(DateTimeOffset grantedAt)
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        GrantedAt = grantedAt;
        RevokedAt = null;
    }
}
