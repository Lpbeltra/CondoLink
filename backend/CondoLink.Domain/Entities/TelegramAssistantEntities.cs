using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class TelegramUserLink
{
    private TelegramUserLink() { }
    public TelegramUserLink(Guid userId, long telegramUserId, long chatId, DateTime now)
    { Id = Guid.NewGuid(); UserId = userId; TelegramUserId = telegramUserId; TelegramChatId = chatId;
      CreatedAt = VerifiedAt = UpdatedAt = now; IsActive = true; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public long TelegramUserId { get; private set; }
    public long TelegramChatId { get; private set; }
    public Guid? ActiveCondominiumId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime VerifiedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsActive { get; private set; }
    public void Reactivate(long telegramUserId, long chatId, DateTime now)
    { TelegramUserId = telegramUserId; TelegramChatId = chatId; VerifiedAt = UpdatedAt = now; IsActive = true; }
    public void SelectCondominium(Guid condominiumId, DateTime now)
    { ActiveCondominiumId = condominiumId; UpdatedAt = now; }
    public void ClearCondominium(DateTime now) { ActiveCondominiumId = null; UpdatedAt = now; }
    public void Deactivate(DateTime now) { IsActive = false; ActiveCondominiumId = null; UpdatedAt = now; }
}

public sealed class TelegramLinkCode
{
    private TelegramLinkCode() { }
    public TelegramLinkCode(Guid userId, string codeHash, DateTime now, DateTime expiresAt)
    { Id = Guid.NewGuid(); UserId = userId; CodeHash = codeHash; CreatedAt = now; ExpiresAt = expiresAt; }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public DateTime? InvalidatedAt { get; private set; }
    public bool IsUsable(DateTime now) => UsedAt is null && InvalidatedAt is null && ExpiresAt > now;
    public void Use(DateTime now) => UsedAt = now;
    public void Invalidate(DateTime now) => InvalidatedAt ??= now;
}

public sealed class TelegramInboundUpdate
{
    private TelegramInboundUpdate() { }
    public TelegramInboundUpdate(long updateId, long telegramUserId, long chatId, string text, DateTime now)
    { Id = Guid.NewGuid(); UpdateId = updateId; TelegramUserId = telegramUserId; ChatId = chatId;
      Text = text; Status = TelegramInboundStatus.Pending; ReceivedAt = NextAttemptAt = now; }
    public Guid Id { get; private set; }
    public long UpdateId { get; private set; }
    public long TelegramUserId { get; private set; }
    public long ChatId { get; private set; }
    public string Text { get; private set; } = null!;
    public TelegramInboundStatus Status { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? ProcessingStartedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }
    public string? ResponseText { get; private set; }
    public Guid? ConversationId { get; private set; }
    public Guid? UserMessageId { get; private set; }
    public int SentPartCount { get; private set; }
    public Guid? ProcessingToken { get; private set; }
    public void AttachConversation(Guid conversationId, Guid userMessageId)
    { ConversationId = conversationId; UserMessageId = userMessageId; }
    public void PrepareResponse(string response) { ResponseText = response; }
    public void PartSent() => SentPartCount++;
    public void Complete(DateTime now) { Status = TelegramInboundStatus.Completed; ProcessedAt = now; ProcessingToken = null; LastError = null; }
    public void Ignore(DateTime now) { Status = TelegramInboundStatus.Ignored; ProcessedAt = now; ProcessingToken = null; }
    public void Retry(DateTime now, TimeSpan delay, string error, int maximumAttempts)
    { LastError = error; ProcessingToken = null; ProcessingStartedAt = null;
      if (Attempts >= maximumAttempts) { Status = TelegramInboundStatus.Failed; ProcessedAt = now; }
      else { Status = TelegramInboundStatus.Pending; NextAttemptAt = now + delay; } }
}
