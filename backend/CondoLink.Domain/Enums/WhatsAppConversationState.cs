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
    ,ListingOwnRequests = 18
    ,ViewingOwnRequest = 19
    ,ViewingOwnRequestUpdates = 20
    ,AwaitingResidentReplyChoice = 21
    ,CollectingResidentReply = 22
    ,ReviewingResidentReply = 23
    ,CollectingResidentReplyAttachments = 24
    ,CollectingAdminResidentData = 25
    ,SelectingAdminResidentUnit = 26
    ,ConfirmingAdminResident = 27
    ,CorrectingAdminResident = 28
    ,CollectingAdminResidentLookup = 29
    ,SelectingAdminLookupCondominium = 30
    ,SelectingAdminLookupUnit = 31
    ,SelectingAdminLookupResident = 32
}
