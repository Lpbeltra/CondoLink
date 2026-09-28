namespace CondoLink.Domain.Entities;

public sealed class ExternalCondominiumMapping
{
    private ExternalCondominiumMapping() { }

    public ExternalCondominiumMapping(Guid administratorIntegrationId, Guid condominiumId, string externalCondominiumId)
    {
        if (administratorIntegrationId == Guid.Empty || condominiumId == Guid.Empty)
            throw new ArgumentException("Integration and condominium are required.");
        if (string.IsNullOrWhiteSpace(externalCondominiumId))
            throw new ArgumentException("External condominium ID is required.", nameof(externalCondominiumId));
        Id = Guid.NewGuid();
        AdministratorIntegrationId = administratorIntegrationId;
        CondominiumId = condominiumId;
        ExternalCondominiumId = externalCondominiumId.Trim();
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid AdministratorIntegrationId { get; private set; }
    public AdministratorIntegration AdministratorIntegration { get; private set; } = null!;
    public Guid CondominiumId { get; private set; }
    public Condominium Condominium { get; private set; } = null!;
    public string ExternalCondominiumId { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}
