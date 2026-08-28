namespace CondoLink.Domain.Entities;
public sealed class ManagementCompanyRequestAttachment
{
    private ManagementCompanyRequestAttachment() { }
    public ManagementCompanyRequestAttachment(Guid requestId,Guid uploadedByUserId,string originalFileName,string storageKey,string contentType,long fileSize,Guid? messageId=null)
    { if(requestId==Guid.Empty||uploadedByUserId==Guid.Empty)throw new ArgumentException("Request and uploader are required."); if(string.IsNullOrWhiteSpace(originalFileName)||string.IsNullOrWhiteSpace(storageKey)||string.IsNullOrWhiteSpace(contentType)||fileSize<=0)throw new ArgumentException("Attachment metadata is invalid."); Id=Guid.NewGuid();RequestId=requestId;MessageId=messageId;UploadedByUserId=uploadedByUserId;OriginalFileName=originalFileName.Trim();StorageKey=storageKey.Trim();ContentType=contentType.Trim();FileSize=fileSize;CreatedAt=DateTime.UtcNow; }
    public Guid Id{get;private set;} public Guid RequestId{get;private set;} public Guid? MessageId{get;private set;} public Guid UploadedByUserId{get;private set;} public string OriginalFileName{get;private set;}=null!; public string StorageKey{get;private set;}=null!; public string ContentType{get;private set;}=null!; public long FileSize{get;private set;} public DateTime CreatedAt{get;private set;}
}
