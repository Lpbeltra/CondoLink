namespace CondoLink.Domain.Common;

public abstract class Entity : IEquatable<Entity>
{
    protected Entity()
        : this(Guid.NewGuid())
    {
    }

    protected Entity(Guid id)
    {
        Id = id;
    }

    public Guid Id { get; }

    public override bool Equals(object? obj) => Equals(obj as Entity);

    public bool Equals(Entity? other)
    {
        return other is not null
            && GetType() == other.GetType()
            && Id == other.Id;
    }

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);
}
