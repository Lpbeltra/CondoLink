namespace CondoLink.Domain.Entities;

public sealed class OperationalMessageTemplate
{
    private OperationalMessageTemplate() { }

    public OperationalMessageTemplate(string key, string prefix, string suffix,
        Guid updatedByUserId, DateTime updatedAt)
    {
        Key = key;
        Prefix = prefix;
        Suffix = suffix;
        UpdatedByUserId = updatedByUserId;
        UpdatedAt = updatedAt;
    }

    public string Key { get; private set; } = null!;
    public string Prefix { get; private set; } = null!;
    public string Suffix { get; private set; } = null!;
    public DateTime UpdatedAt { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    public void Update(string prefix, string suffix, Guid userId, DateTime now)
    {
        Prefix = prefix;
        Suffix = suffix;
        UpdatedByUserId = userId;
        UpdatedAt = now;
    }
}
