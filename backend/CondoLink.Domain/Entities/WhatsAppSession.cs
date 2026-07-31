using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class WhatsAppSession
{
    private WhatsAppSession() { }

    public WhatsAppSession(string phoneNumber, DateTime now, DateTime expiresAt)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));
        Id = Guid.NewGuid();
        PhoneNumber = phoneNumber.Trim();
        State = WhatsAppConversationState.MainMenu;
        LastInteractionAt = now;
        ExpiresAt = expiresAt;
        Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public string PhoneNumber { get; private set; } = null!;
    public Guid? UserId { get; private set; }
    public Guid? CondominiumId { get; private set; }
    public Guid? UnitId { get; private set; }
    public Guid? RequestId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string? DraftDescription { get; private set; }
    public int Page { get; private set; }
    public WhatsAppConversationState State { get; private set; }
    public WhatsAppConversationState? PreviousState { get; private set; }
    public DateTime LastInteractionAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public Guid Version { get; private set; }

    public void Identify(Guid userId) => UserId = userId;

    public void ResolveContext(Guid userId, Guid condominiumId, Guid unitId)
    {
        UserId = userId;
        CondominiumId = condominiumId;
        UnitId = unitId;
    }

    public void RecoverContext(
        Guid userId,
        Guid condominiumId,
        Guid unitId,
        DateTime now,
        DateTime expiresAt)
    {
        UserId = userId;
        CondominiumId = condominiumId;
        UnitId = unitId;
        RequestId = null;
        CategoryId = null;
        DraftDescription = null;
        Page = 0;
        MoveTo(WhatsAppConversationState.MainMenu, now, expiresAt, condominiumId);
    }

    public void InvalidateIdentity(DateTime now, DateTime expiresAt)
    {
        UserId = null;
        CondominiumId = null;
        UnitId = null;
        RequestId = null;
        CategoryId = null;
        DraftDescription = null;
        MoveTo(WhatsAppConversationState.UnknownPhone, now, expiresAt);
    }

    public void Touch(DateTime now, DateTime expiresAt)
    {
        LastInteractionAt = now;
        ExpiresAt = expiresAt;
        Version = Guid.NewGuid();
    }

    public void Restart(DateTime now, DateTime expiresAt)
    {
        PreviousState = State;
        State = WhatsAppConversationState.MainMenu;
        RequestId = null;
        CategoryId = null;
        DraftDescription = null;
        Page = 0;
        Touch(now, expiresAt);
    }

    public void BeginDescription(DateTime now, DateTime expiresAt, bool clearDescription = false)
    {
        CategoryId = null;
        RequestId = null;
        if (clearDescription) DraftDescription = null;
        MoveTo(WhatsAppConversationState.CollectingDescription, now, expiresAt, CondominiumId);
    }

    public void SetDescriptionForReview(string description, DateTime now, DateTime expiresAt)
    {
        DraftDescription = description.Trim();
        MoveTo(WhatsAppConversationState.ReviewingNewRequest, now, expiresAt, CondominiumId);
    }

    public void BeginCategorySelection(DateTime now, DateTime expiresAt) =>
        MoveTo(WhatsAppConversationState.SelectingCategory, now, expiresAt, CondominiumId);

    public void ChooseCategory(Guid categoryId, DateTime now, DateTime expiresAt)
    {
        CategoryId = categoryId;
        Touch(now, expiresAt);
    }

    public void CompleteRequest(Guid requestId, DateTime now, DateTime expiresAt)
    {
        PreviousState = State;
        State = WhatsAppConversationState.MainMenu;
        RequestId = null;
        CategoryId = null;
        DraftDescription = null;
        Page = 0;
        Touch(now, expiresAt);
    }

    public void MoveTo(
        WhatsAppConversationState state,
        DateTime now,
        DateTime expiresAt,
        Guid? condominiumId = null)
    {
        PreviousState = State;
        State = state;
        CondominiumId = condominiumId;
        LastInteractionAt = now;
        ExpiresAt = expiresAt;
        Version = Guid.NewGuid();
    }

    public void SelectUnit(Guid? unitId, DateTime now, DateTime expiresAt)
    {
        UnitId = unitId;
        MoveTo(WhatsAppConversationState.SelectingCategory, now, expiresAt, CondominiumId);
    }

    public void SelectCategory(Guid categoryId, DateTime now, DateTime expiresAt)
    {
        CategoryId = categoryId;
        MoveTo(WhatsAppConversationState.CollectingDescription, now, expiresAt, CondominiumId);
    }

    public void SetDescription(string description, DateTime now, DateTime expiresAt)
    {
        DraftDescription = description.Trim();
        MoveTo(WhatsAppConversationState.CollectingNewRequestAttachments, now, expiresAt, CondominiumId);
    }

    public void SelectRequest(Guid requestId, DateTime now, DateTime expiresAt)
    {
        RequestId = requestId;
        MoveTo(WhatsAppConversationState.ViewingRequest, now, expiresAt, CondominiumId);
    }

    public void SetPage(int page, DateTime now, DateTime expiresAt)
    {
        Page = Math.Max(0, page);
        LastInteractionAt = now;
        ExpiresAt = expiresAt;
        Version = Guid.NewGuid();
    }

    public bool HasDraft => CategoryId.HasValue || !string.IsNullOrWhiteSpace(DraftDescription);

    public void ClearDraft()
    {
        UnitId = null;
        CategoryId = null;
        DraftDescription = null;
        RequestId = null;
        Page = 0;
        Version = Guid.NewGuid();
    }

    public void ReturnToPrevious(DateTime now, DateTime expiresAt)
    {
        var target = PreviousState ?? WhatsAppConversationState.MainMenu;
        PreviousState = State;
        State = target;
        LastInteractionAt = now;
        ExpiresAt = expiresAt;
        Version = Guid.NewGuid();
    }

    public void Reset(DateTime now, DateTime expiresAt)
    {
        PreviousState = State;
        State = WhatsAppConversationState.MainMenu;
        CondominiumId = null;
        UnitId = null;
        RequestId = null;
        CategoryId = null;
        DraftDescription = null;
        Page = 0;
        LastInteractionAt = now;
        ExpiresAt = expiresAt;
        Version = Guid.NewGuid();
    }

    public void End(DateTime now)
    {
        PreviousState = State;
        State = WhatsAppConversationState.Ended;
        CondominiumId = null;
        UnitId = null;
        RequestId = null;
        CategoryId = null;
        DraftDescription = null;
        Page = 0;
        LastInteractionAt = now;
        ExpiresAt = now;
        Version = Guid.NewGuid();
    }
}
