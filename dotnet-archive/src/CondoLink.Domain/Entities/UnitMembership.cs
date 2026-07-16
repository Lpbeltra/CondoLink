using CondoLink.Domain.Common;
using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public class UnitMembership : AuditableEntity, IAggregateRoot
{
    protected UnitMembership()
    {
    }

    public UnitMembership(
        Guid userId,
        Guid unitId,
        UnitRelationshipType relationshipType,
        bool isResident,
        bool isPrimaryResidence)
    {
        UserId = Guard.AgainstDefault(userId, nameof(userId));
        UnitId = Guard.AgainstDefault(unitId, nameof(unitId));
        RelationshipType = Guard.AgainstInvalidEnum(
            relationshipType,
            nameof(relationshipType));
        EnsureResidenceConsistency(isResident, isPrimaryResidence);

        IsResident = isResident;
        IsPrimaryResidence = isPrimaryResidence;
        IsActive = true;
        StartedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }

    public Guid UnitId { get; private set; }

    public UnitRelationshipType RelationshipType { get; private set; }

    public bool IsResident { get; private set; }

    public bool IsPrimaryResidence { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public void ChangeRelationshipType(UnitRelationshipType relationshipType)
    {
        var validatedRelationshipType = Guard.AgainstInvalidEnum(
            relationshipType,
            nameof(relationshipType));
        EnsureIsActive();

        if (RelationshipType == validatedRelationshipType)
        {
            return;
        }

        RelationshipType = validatedRelationshipType;
        Touch();
    }

    public void ChangeResidenceStatus(
        bool isResident,
        bool isPrimaryResidence)
    {
        EnsureIsActive();
        EnsureResidenceConsistency(isResident, isPrimaryResidence);

        if (IsResident == isResident
            && IsPrimaryResidence == isPrimaryResidence)
        {
            return;
        }

        IsResident = isResident;
        IsPrimaryResidence = isPrimaryResidence;
        Touch();
    }

    public void MarkAsPrimaryResidence()
    {
        EnsureIsActive();

        if (!IsResident)
        {
            throw new DomainException(
                "Apenas um residente pode definir a unidade como residência principal.");
        }

        if (IsPrimaryResidence)
        {
            return;
        }

        IsPrimaryResidence = true;
        Touch();
    }

    public void RemovePrimaryResidence()
    {
        EnsureIsActive();

        if (!IsPrimaryResidence)
        {
            return;
        }

        IsPrimaryResidence = false;
        Touch();
    }

    public void End()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        EndedAt = DateTimeOffset.UtcNow;
        IsPrimaryResidence = false;
        Touch();
    }

    public void Reactivate(
        UnitRelationshipType relationshipType,
        bool isResident,
        bool isPrimaryResidence)
    {
        if (IsActive)
        {
            throw new DomainException("O vínculo com a unidade já está ativo.");
        }

        var validatedRelationshipType = Guard.AgainstInvalidEnum(
            relationshipType,
            nameof(relationshipType));
        EnsureResidenceConsistency(isResident, isPrimaryResidence);

        RelationshipType = validatedRelationshipType;
        IsResident = isResident;
        IsPrimaryResidence = isPrimaryResidence;
        IsActive = true;
        EndedAt = null;
        Touch();
    }

    private void EnsureIsActive()
    {
        if (!IsActive)
        {
            throw new DomainException(
                "Não é possível alterar um vínculo com unidade encerrado.");
        }
    }

    private static void EnsureResidenceConsistency(
        bool isResident,
        bool isPrimaryResidence)
    {
        if (isPrimaryResidence && !isResident)
        {
            throw new DomainException(
                "Uma residência principal exige que o usuário seja residente da unidade.");
        }
    }
}
