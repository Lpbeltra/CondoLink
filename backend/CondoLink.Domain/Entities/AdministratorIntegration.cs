namespace CondoLink.Domain.Entities;

public sealed class AdministratorIntegration
{
    private AdministratorIntegration() { }
    public AdministratorIntegration(Guid administratorId, string provider, string appToken, string accessToken, string secret)
    {
        Id = Guid.NewGuid(); AdministratorId = administratorId; Provider = provider;
        SetCredentials(appToken, accessToken, secret); Status = "NotConfigured";
        CreatedAt = UpdatedAt = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid AdministratorId { get; private set; }
    public ManagementCompany Administrator { get; private set; } = null!;
    public string Provider { get; private set; } = null!;
    public string Status { get; private set; } = null!;
    public string EncryptedAppToken { get; private set; } = null!;
    public string EncryptedAccessToken { get; private set; } = null!;
    public string EncryptedSecret { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? LastValidatedAt { get; private set; }
    public void SetCredentials(string appToken, string accessToken, string secret)
    { EncryptedAppToken = appToken; EncryptedAccessToken = accessToken; EncryptedSecret = secret; UpdatedAt = DateTime.UtcNow; }
    public void SetValidation(string status, DateTime? validatedAt = null)
    { Status = status; LastValidatedAt = validatedAt ?? LastValidatedAt; UpdatedAt = DateTime.UtcNow; }
}
