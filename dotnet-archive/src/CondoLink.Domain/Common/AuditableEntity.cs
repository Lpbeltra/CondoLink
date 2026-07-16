namespace CondoLink.Domain.Common;

public abstract class AuditableEntity : Entity
{
    protected AuditableEntity()
    {
        CreatedAt = DateTimeOffset.UtcNow;
    }

    protected AuditableEntity(Guid id)
        : base(id)
    {
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }
    protected void Touch()
    {
        UpdatedAt = DateTimeOffset.UtcNow;
    }

}
