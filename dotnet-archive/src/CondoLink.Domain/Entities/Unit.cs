using CondoLink.Domain.Common;

namespace CondoLink.Domain.Entities;

public class Unit : AuditableEntity
{
    protected Unit()
    {
        Identifier = string.Empty;
        Condominium = null!;
    }

    internal Unit(Condominium condominium, string identifier, string? block)
    {
        Condominium = Guard.AgainstNull(condominium, nameof(condominium));
        CondominiumId = condominium.Id;
        Identifier = Guard.AgainstNullOrWhiteSpace(identifier, nameof(identifier));
        Block = NormalizeOptionalText(block);
        IsActive = true;
    }

    public Guid CondominiumId { get; private set; }

    public Condominium Condominium { get; private set; }

    public string Identifier { get; private set; }

    public string? Block { get; private set; }

    public string? Floor { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public void ChangeIdentifier(string identifier)
    {
        var normalizedIdentifier = Guard.AgainstNullOrWhiteSpace(
            identifier,
            nameof(identifier));

        if (Identifier == normalizedIdentifier)
        {
            return;
        }

        Condominium.EnsureUniqueUnitIdentification(
            normalizedIdentifier,
            Block,
            this);

        Identifier = normalizedIdentifier;
        Touch();
    }

    public void ChangeLocation(string? block, string? floor)
    {
        var normalizedBlock = NormalizeOptionalText(block);
        var normalizedFloor = NormalizeOptionalText(floor);

        if (Block == normalizedBlock && Floor == normalizedFloor)
        {
            return;
        }

        Condominium.EnsureUniqueUnitIdentification(
            Identifier,
            normalizedBlock,
            this);

        Block = normalizedBlock;
        Floor = normalizedFloor;
        Touch();
    }

    public void ChangeDescription(string? description)
    {
        var normalizedDescription = NormalizeOptionalText(description);

        if (Description == normalizedDescription)
        {
            return;
        }

        Description = normalizedDescription;
        Touch();
    }

    public void Activate()
    {
        if (IsActive)
        {
            return;
        }

        if (!Condominium.IsActive)
        {
            throw new DomainException(
                "Não é possível ativar uma unidade de um condomínio inativo.");
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

    private static string? NormalizeOptionalText(string? value)
    {
        var normalizedValue = value?.Trim();
        return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
    }
}
