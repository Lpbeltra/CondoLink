namespace CondoLink.Domain.Enums;

public enum WhatsAppConversationState
{
    MainMenu = 1,
    SelectingCondominium = 2,
    StartingNewRequest = 3,
    SelectingOpenRequest = 4,
    UnknownPhone = 5,
    AmbiguousPhone = 6,
    Ended = 7
    ,SelectingUnit = 8
    ,SelectingCategory = 9
    ,CollectingDescription = 10
    ,CollectingAttachments = 11
    ,CollectingNewRequestAttachments = CollectingAttachments
    ,ReviewingNewRequest = 12
    ,ViewingRequest = 13
    ,ReplyingToRequest = 14
    ,CollectingExistingRequestAttachment = 15
    ,ViewingRequestHistory = 16
    ,ConfirmingResume = 17
}
