using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class WhatsAppPhoneVerification
{
    private WhatsAppPhoneVerification() { }

    public WhatsAppPhoneVerification(
        Guid userId, string normalizedPhoneNumber, byte[] codeHash,
        byte[] codeSalt, DateTime now, DateTime expiresAt, int maximumAttempts,
        WhatsAppChallengePurpose purpose =
            WhatsAppChallengePurpose.PhoneVerification)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(normalizedPhoneNumber))
            throw new ArgumentException("Phone is required.", nameof(normalizedPhoneNumber));
        if (codeHash.Length == 0) throw new ArgumentException("Code hash is required.", nameof(codeHash));
        if (codeSalt.Length == 0) throw new ArgumentException("Code salt is required.", nameof(codeSalt));
        if (expiresAt <= now) throw new ArgumentException("Expiration must be in the future.", nameof(expiresAt));
        if (maximumAttempts < 1) throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

        Id = Guid.NewGuid();
        UserId = userId;
        NormalizedPhoneNumber = normalizedPhoneNumber;
        CodeHash = codeHash;
        CodeSalt = codeSalt;
        CreatedAt = now;
        UpdatedAt = now;
        ExpiresAt = expiresAt;
        MaximumAttempts = maximumAttempts;
        Purpose = purpose;
        Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string NormalizedPhoneNumber { get; private set; } = null!;
    public byte[] CodeHash { get; private set; } = null!;
    public byte[] CodeSalt { get; private set; } = null!;
    public int AttemptCount { get; private set; }
    public int MaximumAttempts { get; private set; }
    public WhatsAppChallengePurpose Purpose { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }
    public DateTime? InvalidatedAt { get; private set; }
    public Guid Version { get; private set; }

    public bool IsPending(DateTime now) =>
        ConsumedAt is null && InvalidatedAt is null
        && ExpiresAt > now && AttemptCount < MaximumAttempts;

    public bool RegisterFailedAttempt(DateTime now)
    {
        if (!IsPending(now)) return false;
        AttemptCount++;
        UpdatedAt = now;
        if (AttemptCount >= MaximumAttempts) InvalidatedAt = now;
        Version = Guid.NewGuid();
        return true;
    }

    public void Confirm(DateTime now)
    {
        if (!IsPending(now)) throw new InvalidOperationException("Verification is not pending.");
        ConfirmedAt = now;
        ConsumedAt = now;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }

    public void Consume(DateTime now)
    {
        if (!IsPending(now)) throw new InvalidOperationException("Challenge is not pending.");
        ConsumedAt = now;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }

    public void Invalidate(DateTime now)
    {
        if (ConsumedAt is not null || InvalidatedAt is not null) return;
        InvalidatedAt = now;
        UpdatedAt = now;
        Version = Guid.NewGuid();
    }
}
