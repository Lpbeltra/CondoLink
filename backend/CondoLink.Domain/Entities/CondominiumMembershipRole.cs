using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class CondominiumMembershipRole
{
    private CondominiumMembershipRole()
    {
    }

    public CondominiumMembershipRole(
        Guid condominiumMembershipId,
        CondominiumRole role)
    {
        if (condominiumMembershipId == Guid.Empty)
        {
            throw new ArgumentException(
                "CondominiumMembershipId is required.",
                nameof(condominiumMembershipId));
        }

        if (!Enum.IsDefined(role))
        {
            throw new ArgumentOutOfRangeException(nameof(role), "Role is invalid.");
        }

        Id = Guid.NewGuid();
        CondominiumMembershipId = condominiumMembershipId;
        Role = role;
        IsActive = true;
        GrantedAt = DateTime.UtcNow;
        RevokedAt = null;
    }

    public Guid Id { get; private set; }
    public Guid CondominiumMembershipId { get; private set; }
    public CondominiumRole Role { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime GrantedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
}
