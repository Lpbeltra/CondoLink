namespace CondoLink.Domain.Entities;

/// <summary>Ownership ledger for an explicitly provisioned commercial demo.</summary>
public sealed class CommercialDemoDataset
{
    private CommercialDemoDataset() { }

    public CommercialDemoDataset(string key, string manifestJson, Guid createdByUserId)
    {
        Key = key;
        ManifestJson = manifestJson;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
    }

    public string Key { get; private set; } = null!;
    public string ManifestJson { get; private set; } = null!;
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void SetManifest(string manifestJson) => ManifestJson = manifestJson;
}
