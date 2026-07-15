using CondoLink.Domain.Common;

namespace CondoLink.Domain.Entities;

public class Category : AuditableEntity, IAggregateRoot
{
    protected Category()
    {
        Name = string.Empty;
    }

    public Category(Guid condominiumId, string name, string? description)
    {
        CondominiumId = Guard.AgainstDefault(
            condominiumId,
            nameof(condominiumId));
        Name = Guard.AgainstNullOrWhiteSpace(name, nameof(name));
        Description = NormalizeDescription(description);
        IsActive = true;
    }

    public Guid CondominiumId { get; private set; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public void ChangeName(string name)
    {
        var normalizedName = Guard.AgainstNullOrWhiteSpace(name, nameof(name));

        if (string.Equals(Name, normalizedName, StringComparison.Ordinal))
        {
            return;
        }

        Name = normalizedName;
        Touch();
    }

    public void ChangeDescription(string? description)
    {
        var normalizedDescription = NormalizeDescription(description);

        if (string.Equals(
                Description,
                normalizedDescription,
                StringComparison.Ordinal))
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

    private static string? NormalizeDescription(string? description)
    {
        var normalizedDescription = description?.Trim();

        return string.IsNullOrWhiteSpace(normalizedDescription)
            ? null
            : normalizedDescription;
    }
}
