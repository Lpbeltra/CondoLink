namespace CondoLink.Domain.Entities;

public sealed class RequestServiceProviderHistory
{
    private RequestServiceProviderHistory() { }
    public RequestServiceProviderHistory(Guid requestId, string eventType, string? previousName, string? previousSpecialty, string? providerName, string? providerSpecialty, Guid changedByUserId, DateTime createdAt)
    { Id=Guid.NewGuid(); RequestId=requestId; EventType=eventType; PreviousName=previousName; PreviousSpecialty=previousSpecialty; ProviderName=providerName; ProviderSpecialty=providerSpecialty; ChangedByUserId=changedByUserId; CreatedAt=createdAt; }
    public Guid Id { get; private set; } public Guid RequestId { get; private set; } public string EventType { get; private set; } = null!; public string? PreviousName { get; private set; } public string? PreviousSpecialty { get; private set; } public string? ProviderName { get; private set; } public string? ProviderSpecialty { get; private set; } public Guid ChangedByUserId { get; private set; } public DateTime CreatedAt { get; private set; }
}
