namespace CondoLink.Domain.Entities;

public sealed class Unit
{
    private Unit()
    {
    }

    public Unit(
        Guid condominiumId,
        string identifier,
        Guid? blockId,
        string? floor,
        string? description)
    {
        if (condominiumId == Guid.Empty)
        {
            throw new ArgumentException("CondominiumId is required.", nameof(condominiumId));
        }

        if (string.IsNullOrWhiteSpace(identifier))
        {
            throw new ArgumentException("Identifier is required.", nameof(identifier));
        }

        var now = DateTime.UtcNow;

        Id = Guid.NewGuid();
        CondominiumId = condominiumId;
        Identifier = identifier.Trim();
        BlockId = blockId;
        Floor = NormalizeOptional(floor);
        Description = NormalizeOptional(description);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public string Identifier { get; private set; } = null!;
    public Guid? BlockId { get; private set; }
    public string? Floor { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Update(string identifier, Guid? blockId, string? description)
    {
        if (string.IsNullOrWhiteSpace(identifier)) throw new ArgumentException("Identifier is required.", nameof(identifier));
        Identifier = identifier.Trim(); BlockId = blockId; Description = NormalizeOptional(description); UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
