namespace CondoLink.Infrastructure.Identity;

public sealed class RefreshSession
{
    private RefreshSession() { }
    public RefreshSession(Guid userId, string tokenHash, string securityStamp, DateTime createdAt, DateTime expiresAt)
    { Id = Guid.NewGuid(); UserId = userId; TokenHash = tokenHash; SecurityStamp = securityStamp; CreatedAt = createdAt; ExpiresAt = expiresAt; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; } = null!;
    public string SecurityStamp { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }
}
