using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class UnitMembership
{
    private UnitMembership()
    {
    }

    public UnitMembership(
        Guid userId,
        Guid unitId,
        UnitRelationshipType relationshipType,
        bool isResident,
        bool isPrimaryResidence)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        if (unitId == Guid.Empty)
        {
            throw new ArgumentException("UnitId is required.", nameof(unitId));
        }

        if (!Enum.IsDefined(relationshipType))
        {
            throw new ArgumentOutOfRangeException(
                nameof(relationshipType),
                "Relationship type is invalid.");
        }

        if (isPrimaryResidence && !isResident)
        {
            throw new ArgumentException(
                "Primary residence requires the user to be a resident.",
                nameof(isPrimaryResidence));
        }

        var now = DateTime.UtcNow;

        Id = Guid.NewGuid();
        UserId = userId;
        UnitId = unitId;
        RelationshipType = relationshipType;
        IsResident = isResident;
        IsPrimaryResidence = isPrimaryResidence;
        IsActive = true;
        StartedAt = now;
        EndedAt = null;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid UnitId { get; private set; }
    public UnitRelationshipType RelationshipType { get; private set; }
    public bool IsResident { get; private set; }
    public bool IsPrimaryResidence { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
