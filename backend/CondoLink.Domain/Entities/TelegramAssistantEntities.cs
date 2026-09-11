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
    public long? PendingTelegramUserId { get; private set; }
    public long? PendingTelegramChatId { get; private set; }
    public DateTime? VerificationStartedAt { get; private set; }
    public bool IsUsable(DateTime now) => UsedAt is null && InvalidatedAt is null && ExpiresAt > now;
    public bool BeginPhoneVerification(long telegramUserId, long chatId, DateTime now)
    {
        if (PendingTelegramUserId.HasValue
            && (PendingTelegramUserId != telegramUserId || PendingTelegramChatId != chatId)) return false;
        PendingTelegramUserId = telegramUserId; PendingTelegramChatId = chatId; VerificationStartedAt = now;
        return true;
    }
    public bool IsPendingFor(long telegramUserId, long chatId, DateTime now) => IsUsable(now)
        && PendingTelegramUserId == telegramUserId && PendingTelegramChatId == chatId;
    public void Use(DateTime now) => UsedAt = now;
    public void Invalidate(DateTime now) => InvalidatedAt ??= now;
}

public sealed class TelegramInboundUpdate
{
    private TelegramInboundUpdate() { }
    public TelegramInboundUpdate(long updateId, long telegramUserId, long chatId, string text, DateTime now)
    { Id = Guid.NewGuid(); UpdateId = updateId; TelegramUserId = telegramUserId; ChatId = chatId;
      Text = text; Kind = TelegramInboundKind.Text; Status = TelegramInboundStatus.Pending; ReceivedAt = NextAttemptAt = now; }
    public TelegramInboundUpdate(long updateId, long telegramUserId, long chatId,
        TelegramInboundKind kind, DateTime now, string? contactPhoneNumber = null,
        long? contactUserId = null, string? fileId = null, long? fileSize = null,
        int? durationSeconds = null, string? fileName = null, string? mimeType = null)
    { Id = Guid.NewGuid(); UpdateId = updateId; TelegramUserId = telegramUserId; ChatId = chatId;
      Text = string.Empty; Kind = kind; ContactPhoneNumber = contactPhoneNumber;
      ContactUserId = contactUserId; FileId = fileId; FileSize = fileSize;
      DurationSeconds = durationSeconds; FileName = fileName; MimeType = mimeType;
      Status = TelegramInboundStatus.Pending; ReceivedAt = NextAttemptAt = now; }
    public Guid Id { get; private set; }
    public long UpdateId { get; private set; }
    public long TelegramUserId { get; private set; }
    public long ChatId { get; private set; }
    public string Text { get; private set; } = null!;
    public TelegramInboundKind Kind { get; private set; }
    public string? ContactPhoneNumber { get; private set; }
    public long? ContactUserId { get; private set; }
    public string? FileId { get; private set; }
    public long? FileSize { get; private set; }
    public int? DurationSeconds { get; private set; }
    public string? FileName { get; private set; }
    public string? MimeType { get; private set; }
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
    public TelegramReplyMarkup ReplyMarkup { get; private set; }
    public Guid? AssistantExecutionId { get; private set; }
    public long? QueueDurationMs { get; private set; }
    public long? AuthorizationDurationMs { get; private set; }
    public long? AudioDownloadDurationMs { get; private set; }
    public long? TranscriptionDurationMs { get; private set; }
    public long? AssistantDurationMs { get; private set; }
    public long? DeliveryDurationMs { get; private set; }
    public long? TotalDurationMs { get; private set; }
    public void AttachConversation(Guid conversationId, Guid userMessageId)
    { ConversationId = conversationId; UserMessageId = userMessageId; }
    public void SetTranscribedText(string text) { Text = text; }
    public void PrepareResponse(string response, TelegramReplyMarkup replyMarkup = TelegramReplyMarkup.None)
    { ResponseText = response; ReplyMarkup = replyMarkup; }
    public void AttachAssistantExecution(Guid executionId) { AssistantExecutionId = executionId; }
    public void RecordQueue(long milliseconds) { QueueDurationMs = Math.Max(0, milliseconds); }
    public void RecordAuthorization(long milliseconds) { AuthorizationDurationMs = Math.Max(0, milliseconds); }
    public void RecordAudioDownload(long milliseconds) { AudioDownloadDurationMs = Math.Max(0, milliseconds); }
    public void RecordTranscription(long milliseconds) { TranscriptionDurationMs = Math.Max(0, milliseconds); }
    public void RecordAssistant(long milliseconds) { AssistantDurationMs = Math.Max(0, milliseconds); }
    public void RecordDelivery(long milliseconds) { DeliveryDurationMs = Math.Max(0, milliseconds); }
    public void RecordTotal(long milliseconds) { TotalDurationMs = Math.Max(0, milliseconds); }
    public void PartSent() => SentPartCount++;
    public void Complete(DateTime now) { Status = TelegramInboundStatus.Completed; ProcessedAt = now; ProcessingToken = null; LastError = null; ClearTransientPayload(); }
    public void Ignore(DateTime now) { Status = TelegramInboundStatus.Ignored; ProcessedAt = now; ProcessingToken = null; ClearTransientPayload(); }
    public void Retry(DateTime now, TimeSpan delay, string error, int maximumAttempts)
    { LastError = error; ProcessingToken = null; ProcessingStartedAt = null;
      if (Attempts >= maximumAttempts) { Status = TelegramInboundStatus.Failed; ProcessedAt = now; ClearTransientPayload(); }
      else { Status = TelegramInboundStatus.Pending; NextAttemptAt = now + delay; } }
    private void ClearTransientPayload()
    { ContactPhoneNumber = null; FileId = null; FileName = null; }
}
