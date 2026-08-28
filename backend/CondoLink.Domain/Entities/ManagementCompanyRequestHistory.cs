using CondoLink.Domain.Enums;
namespace CondoLink.Domain.Entities;
public sealed class ManagementCompanyRequestHistory
{
    private ManagementCompanyRequestHistory() { }
    public ManagementCompanyRequestHistory(Guid requestId,ManagementCompanyRequestEventType eventType,ManagementCompanyRequestStatus? previousStatus,ManagementCompanyRequestStatus newStatus,Guid changedByUserId,string? reason,DateTime createdAt)
    { if(requestId==Guid.Empty||changedByUserId==Guid.Empty)throw new ArgumentException("Request and actor are required."); Id=Guid.NewGuid();RequestId=requestId;EventType=eventType;PreviousStatus=previousStatus;NewStatus=newStatus;ChangedByUserId=changedByUserId;Reason=string.IsNullOrWhiteSpace(reason)?null:reason.Trim();CreatedAt=createdAt; }
    public Guid Id{get;private set;} public Guid RequestId{get;private set;} public ManagementCompanyRequestEventType EventType{get;private set;} public ManagementCompanyRequestStatus? PreviousStatus{get;private set;} public ManagementCompanyRequestStatus NewStatus{get;private set;} public Guid ChangedByUserId{get;private set;} public string? Reason{get;private set;} public DateTime CreatedAt{get;private set;}
}
