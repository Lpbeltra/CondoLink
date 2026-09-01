namespace CondoLink.Domain.Entities;
public sealed class ManagementCompanyRequestMessage
{
    private ManagementCompanyRequestMessage() { }
    public ManagementCompanyRequestMessage(Guid requestId,Guid authorUserId,string content)
    { if(requestId==Guid.Empty||authorUserId==Guid.Empty)throw new ArgumentException("Request and author are required."); if(string.IsNullOrWhiteSpace(content)||content.Trim().Length>4000)throw new ArgumentException("Message is required and must not exceed 4000 characters."); Id=Guid.NewGuid();RequestId=requestId;AuthorUserId=authorUserId;Content=content.Trim();CreatedAt=DateTime.UtcNow; }
    public Guid Id{get;private set;} public Guid RequestId{get;private set;} public Guid AuthorUserId{get;private set;} public string Content{get;private set;}=null!; public DateTime CreatedAt{get;private set;}
    public void UpdateContent(string content)
    { if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > 4000) throw new ArgumentException("Message is required and must not exceed 4000 characters."); Content = content.Trim(); }
}
