using System.Collections.ObjectModel;
using CondoLink.Domain.Common;
using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public class CondominiumMembership : AuditableEntity, IAggregateRoot
{
    private readonly List<CondominiumMembershipRole> _roles = [];

    protected CondominiumMembership()
    {
    }

    public CondominiumMembership(Guid userId, Guid condominiumId)
    {
        UserId = Guard.AgainstDefault(userId, nameof(userId));
        CondominiumId = Guard.AgainstDefault(
            condominiumId,
            nameof(condominiumId));
        IsActive = true;
        JoinedAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; private set; }

    public Guid CondominiumId { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    public IReadOnlyCollection<CondominiumMembershipRole> Roles =>
        new ReadOnlyCollection<CondominiumMembershipRole>(_roles);

    public void GrantRole(CondominiumRole role)
    {
        var validatedRole = Guard.AgainstInvalidEnum(role, nameof(role));
        EnsureIsActive();

        if (FindRole(validatedRole) is not null)
        {
            throw new DomainException(
                "Este papel já foi registrado para o vínculo.");
        }

        _roles.Add(new CondominiumMembershipRole(this, validatedRole));
        Touch();
    }

    public void RevokeRole(CondominiumRole role)
    {
        var validatedRole = Guard.AgainstInvalidEnum(role, nameof(role));
        var membershipRole = FindRole(validatedRole)
            ?? throw new DomainException(
                "O papel informado não está registrado para o vínculo.");

        if (!membershipRole.IsActive)
        {
            return;
        }

        membershipRole.Revoke(DateTimeOffset.UtcNow);
        Touch();
    }

    public void ReactivateRole(CondominiumRole role)
    {
        var validatedRole = Guard.AgainstInvalidEnum(role, nameof(role));
        EnsureIsActive();

        var membershipRole = FindRole(validatedRole)
            ?? throw new DomainException(
                "O papel informado não está registrado para o vínculo.");

        if (membershipRole.IsActive)
        {
            return;
        }

        membershipRole.Reactivate(DateTimeOffset.UtcNow);
        Touch();
    }

    public void End()
    {
        if (!IsActive)
        {
            return;
        }

        var endedAt = DateTimeOffset.UtcNow;

        IsActive = false;
        EndedAt = endedAt;

        foreach (var role in _roles.Where(role => role.IsActive))
        {
            role.Revoke(endedAt);
        }

        Touch();
    }

    public void Reactivate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        EndedAt = null;
        Touch();
    }

    private CondominiumMembershipRole? FindRole(CondominiumRole role)
    {
        return _roles.SingleOrDefault(membershipRole =>
            membershipRole.Role == role);
    }

    private void EnsureIsActive()
    {
        if (!IsActive)
        {
            throw new DomainException(
                "Não é possível alterar papéis de um vínculo encerrado.");
        }
    }
}
