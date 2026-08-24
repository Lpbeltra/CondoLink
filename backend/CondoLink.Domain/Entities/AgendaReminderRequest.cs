namespace CondoLink.Domain.Entities;

public sealed class AgendaReminderRequest
{
    private AgendaReminderRequest() { }
    public AgendaReminderRequest(Guid reminderId, Guid requestId, Guid linkedByUserId,
        DateTime linkedAt)
    { Id = Guid.NewGuid(); ReminderId = reminderId; RequestId = requestId;
      LinkedByUserId = linkedByUserId; LinkedAt = linkedAt; }
    public Guid Id { get; private set; }
    public Guid ReminderId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid LinkedByUserId { get; private set; }
    public DateTime LinkedAt { get; private set; }
}
