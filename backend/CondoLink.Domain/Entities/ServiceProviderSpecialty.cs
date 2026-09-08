namespace CondoLink.Domain.Entities;

public sealed class ServiceProviderSpecialty
{
    private ServiceProviderSpecialty() { }
    public ServiceProviderSpecialty(Guid serviceProviderId, string name)
    { Id = Guid.NewGuid(); ServiceProviderId = serviceProviderId; Name = name.Trim(); NormalizedName = Name.ToUpperInvariant(); }
    public Guid Id { get; private set; }
    public Guid ServiceProviderId { get; private set; }
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
}
