namespace CondoLink.Domain.Entities;

public sealed class ServiceProviderCondominiumLink
{
    private ServiceProviderCondominiumLink() { }
    public ServiceProviderCondominiumLink(Guid serviceProviderId, Guid condominiumId) { Id = Guid.NewGuid(); ServiceProviderId = serviceProviderId; CondominiumId = condominiumId; }
    public Guid Id { get; private set; }
    public Guid ServiceProviderId { get; private set; }
    public Guid CondominiumId { get; private set; }
}
