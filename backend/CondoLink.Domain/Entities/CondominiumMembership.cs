namespace CondoLink.Domain.Entities;

public sealed class CondominiumMembership
{
    private CondominiumMembership()
    {
    }

    public CondominiumMembership(Guid userId, Guid condominiumId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("UserId is required.", nameof(userId));
        }

        if (condominiumId == Guid.Empty)
        {
            throw new ArgumentException("CondominiumId is required.", nameof(condominiumId));
        }

        var now = DateTime.UtcNow;

        Id = Guid.NewGuid();
        UserId = userId;
        CondominiumId = condominiumId;
        IsActive = true;
        JoinedAt = now;
        EndedAt = null;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid CondominiumId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
