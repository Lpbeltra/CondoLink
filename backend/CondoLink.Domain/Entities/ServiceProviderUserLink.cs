namespace CondoLink.Domain.Entities;

public sealed class ServiceProviderUserLink
{
    private ServiceProviderUserLink() { }
    public ServiceProviderUserLink(Guid serviceProviderId, Guid userId) { Id = Guid.NewGuid(); ServiceProviderId = serviceProviderId; UserId = userId; }
    public Guid Id { get; private set; }
    public Guid ServiceProviderId { get; private set; }
    public Guid UserId { get; private set; }
}
