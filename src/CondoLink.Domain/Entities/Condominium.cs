using System.Collections.ObjectModel;
using CondoLink.Domain.Common;

namespace CondoLink.Domain.Entities;

public class Condominium : AuditableEntity, IAggregateRoot
{
    private readonly List<Unit> _units = [];

    protected Condominium()
    {
        Name = string.Empty;
        Email = string.Empty;
        PhoneNumber = string.Empty;
    }

    public Condominium(string name, string email, string phoneNumber)
    {
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Email = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        PhoneNumber = Guard.AgainstNullOrWhiteSpace(phoneNumber, nameof(phoneNumber));
        IsActive = true;
    }

    public string Name { get; private set; }

    public string Email { get; private set; }

    public string PhoneNumber { get; private set; }

    public bool IsActive { get; private set; }

    public IReadOnlyCollection<Unit> Units => new ReadOnlyCollection<Unit>(_units);

    public Unit AddUnit(string identifier, string? block = null)
    {
        EnsureIsActive();

        var unit = new Unit(this, identifier, block);
        EnsureUniqueUnitIdentification(unit.Identifier, unit.Block);
        _units.Add(unit);
        Touch();

        return unit;
    }

    public void ChangeName(string name)
    {
        var normalizedName = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

        if (Name == normalizedName)
        {
            return;
        }

        Name = normalizedName;
        Touch();
    }

    public void ChangeContactInformation(string email, string phoneNumber)
    {
        var normalizedEmail = Guard.AgainstNullOrWhiteSpace(email, nameof(email));
        var normalizedPhoneNumber = Guard.AgainstNullOrWhiteSpace(
            phoneNumber,
            nameof(phoneNumber));

        if (Email == normalizedEmail && PhoneNumber == normalizedPhoneNumber)
        {
            return;
        }

        Email = normalizedEmail;
        PhoneNumber = normalizedPhoneNumber;
        Touch();
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        if (!IsActive)
        {
            return;
        }

        IsActive = false;
        Touch();
    }

    private void EnsureIsActive()
    {
        if (!IsActive)
        {
            throw new DomainException(
                "Não é possível adicionar unidades a um condomínio inativo.");
        }
    }

    internal void EnsureUniqueUnitIdentification(
        string identifier,
        string? block,
        Unit? unitToIgnore = null)
    {
        var hasDuplicate = _units.Any(unit =>
            unit != unitToIgnore
            && string.Equals(
                unit.Identifier,
                identifier,
                StringComparison.OrdinalIgnoreCase)
            && string.Equals(
                unit.Block,
                block,
                StringComparison.OrdinalIgnoreCase));

        if (hasDuplicate)
        {
            throw new DomainException(
                "Já existe uma unidade com o mesmo identificador e bloco neste condomínio.");
        }
    }
}
