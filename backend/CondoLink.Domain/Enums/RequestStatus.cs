namespace CondoLink.Domain.Enums;

public enum RequestStatus
{
    Open = 1,
    InProgress = 2,
    WaitingForResident = 3,
    WaitingForThirdParty = 4,
    Resolved = 5,
    Cancelled = 6
}
