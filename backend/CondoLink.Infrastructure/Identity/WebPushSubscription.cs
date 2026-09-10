namespace CondoLink.Infrastructure.Identity;

public sealed class WebPushSubscription
{
    private WebPushSubscription() { }

    public WebPushSubscription(Guid userId, string endpoint, string p256dh,
        string auth, string? userAgent, DateTime now)
    {
        Id = Guid.NewGuid();
        AssignTo(userId, endpoint, p256dh, auth, userAgent, now);
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = null!;
    public string P256dh { get; private set; } = null!;
    public string Auth { get; private set; } = null!;
    public string? UserAgent { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public ApplicationUser User { get; private set; } = null!;

    public void AssignTo(Guid userId, string endpoint, string p256dh,
        string auth, string? userAgent, DateTime now)
    {
        if (userId == Guid.Empty) throw new ArgumentException("UserId is required.", nameof(userId));
        UserId = userId;
        Endpoint = Required(endpoint, nameof(endpoint));
        P256dh = Required(p256dh, nameof(p256dh));
        Auth = Required(auth, nameof(auth));
        UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim();
        IsActive = true;
        UpdatedAt = now;
    }

    public void Deactivate(DateTime now) { IsActive = false; UpdatedAt = now; }
    public void MarkUsed(DateTime now) { LastUsedAt = now; UpdatedAt = now; }

    private static string Required(string value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException($"{name} is required.", name)
            : value.Trim();
}
