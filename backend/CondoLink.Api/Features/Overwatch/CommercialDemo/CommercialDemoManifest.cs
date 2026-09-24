using System.Text.Json;
using CondoLink.Infrastructure.Persistence;

namespace CondoLink.Api.Features.Overwatch.CommercialDemo;

internal sealed class CommercialDemoManifest
{
    public const string DatasetKey = "commercial-demo-v1";
    public Dictionary<string, List<Guid>> Entities { get; init; } = [];
    public List<DemoRoleLink> Roles { get; init; } = [];

    public void Record<T>(T entity, AppDbContext db) where T : class
    {
        var key = typeof(T).FullName!;
        var primaryKey = db.Entry(entity).Metadata.FindPrimaryKey();
        if (primaryKey?.Properties.Count != 1 || primaryKey.Properties[0].ClrType != typeof(Guid))
            throw new InvalidOperationException($"Unsupported demo ownership key: {key}.");
        var id = (Guid)db.Entry(entity).Property(primaryKey.Properties[0].Name).CurrentValue!;
        if (!Entities.TryGetValue(key, out var ids)) Entities[key] = ids = [];
        ids.Add(id);
    }

    public string ToJson() => JsonSerializer.Serialize(this);
    public static CommercialDemoManifest FromJson(string json) =>
        JsonSerializer.Deserialize<CommercialDemoManifest>(json)
        ?? throw new InvalidOperationException("Invalid commercial demo manifest.");
}

internal sealed record DemoRoleLink(Guid UserId, Guid RoleId);
