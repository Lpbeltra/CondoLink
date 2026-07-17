namespace CondoLink.Domain.Entities;

public sealed class CondominiumBlock
{
    private CondominiumBlock() { }

    public CondominiumBlock(Guid condominiumId, string identifier)
    {
        if (condominiumId == Guid.Empty) throw new ArgumentException("CondominiumId is required.", nameof(condominiumId));
        if (string.IsNullOrWhiteSpace(identifier)) throw new ArgumentException("Identifier is required.", nameof(identifier));
        var now = DateTime.UtcNow;
        Id = Guid.NewGuid(); CondominiumId = condominiumId; Identifier = identifier.Trim(); CreatedAt = now; UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public string Identifier { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Rename(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) throw new ArgumentException("Identifier is required.", nameof(identifier));
        Identifier = identifier.Trim(); UpdatedAt = DateTime.UtcNow;
    }
}
