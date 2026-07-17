namespace CondoLink.Domain.Entities;

public sealed class Category
{
    private Category()
    {
    }

    public Category(Guid condominiumId, string name, string? description)
    {
        if (condominiumId == Guid.Empty)
        {
            throw new ArgumentException(
                "CondominiumId is required.",
                nameof(condominiumId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        var now = DateTime.UtcNow;

        Id = Guid.NewGuid();
        CondominiumId = condominiumId;
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
        Description = NormalizeOptional(description);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.", nameof(name));
        Name = name.Trim(); NormalizedName = Name.ToUpperInvariant(); UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
