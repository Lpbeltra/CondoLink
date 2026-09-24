using System.Data;
using System.Data.Common;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace CondoLink.Api.Features.Overwatch.CommercialDemo;

internal sealed class CommercialDemoReverter(AppDbContext db, LocalFileStorage storage,
    ILogger<CommercialDemoReverter> logger)
{
    public async Task<DemoInspection> InspectAsync(CancellationToken ct)
    {
        var ledger = await db.CommercialDemoDatasets.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Key == CommercialDemoManifest.DatasetKey, ct);
        if (ledger is null) return new DemoInspection(false, [], [], 0);
        var manifest = CommercialDemoManifest.FromJson(ledger.ManifestJson);
        ValidateManifest(manifest);
        return await InspectManifestAsync(manifest, ct);
    }

    public async Task<DemoInspection> RevertAsync(Guid operatorId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var ledger = await db.CommercialDemoDatasets.SingleOrDefaultAsync(
            x => x.Key == CommercialDemoManifest.DatasetKey, ct);
        if (ledger is null) return new DemoInspection(false, [], [], 0);
        var manifest = CommercialDemoManifest.FromJson(ledger.ManifestJson);
        ValidateManifest(manifest);
        var inspection = await InspectManifestAsync(manifest, ct);
        if (inspection.Conflicts.Count != 0) return inspection;

        var documents = await db.CondominiumDocuments.AsNoTracking()
            .Where(x => manifest.Entities[typeof(CondominiumDocument).FullName!].Contains(x.Id))
            .Select(x => new { x.CondominiumId, x.Id, x.StorageKey }).ToArrayAsync(ct);
        foreach (var document in documents)
        {
            var expectedPrefix = $"condominium-documents/{document.CondominiumId}/{document.Id}/";
            if (!document.StorageKey.Replace('\\', '/').StartsWith(expectedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Demo document storage ownership is ambiguous.");
        }
        var attachments = await db.RequestAttachments.AsNoTracking()
            .Where(x => manifest.Entities[typeof(RequestAttachment).FullName!].Contains(x.Id))
            .Select(x => new { x.RequestId, x.StorageKey }).ToArrayAsync(ct);
        foreach (var attachment in attachments)
        {
            var expectedPrefix = $"requests/{attachment.RequestId}/";
            if (!attachment.StorageKey.Replace('\\', '/').StartsWith(expectedPrefix, StringComparison.Ordinal))
                throw new InvalidOperationException("Demo attachment storage ownership is ambiguous.");
        }

        foreach (var link in manifest.Roles)
            await ExecuteAsync("DELETE FROM \"AspNetUserRoles\" WHERE \"UserId\" = @p0 AND \"RoleId\" = @p1",
                [link.UserId, link.RoleId], ct);

        foreach (var type in DeleteOrder(manifest))
        {
            var ids = manifest.Entities[type.ClrType.FullName!];
            if (ids.Count == 0) continue;
            var table = Table(type);
            var pk = Column(type, type.FindPrimaryKey()!.Properties[0]);
            await ExecuteAsync($"DELETE FROM {table} WHERE {pk} IN ({Parameters(ids.Count)})", ids, ct);
        }
        db.CommercialDemoDatasets.Remove(ledger);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        foreach (var document in documents)
            storage.DeleteCondominiumDocument(document.CondominiumId, document.Id, document.StorageKey);
        foreach (var attachment in attachments) storage.Delete(attachment.StorageKey);
        logger.LogInformation("Commercial demo {Key} reverted by {OperatorId}; {Count} records removed.",
            CommercialDemoManifest.DatasetKey, operatorId, inspection.Counts.Values.Sum());
        return inspection;
    }

    private async Task<DemoInspection> InspectManifestAsync(CommercialDemoManifest manifest, CancellationToken ct)
    {
        var counts = new Dictionary<string, int>();
        var conflicts = new List<string>();
        var externalRows = new HashSet<string>();
        // Auth artifacts can only belong to their demo user; they are created during
        // normal sign-in after provisioning and therefore cannot be in the original ledger.
        var demoUsers = manifest.Entities[typeof(ApplicationUser).FullName!];
        manifest.Entities[typeof(RefreshSession).FullName!] = await db.RefreshSessions.AsNoTracking()
            .Where(x => demoUsers.Contains(x.UserId)).Select(x => x.Id).ToListAsync(ct);
        manifest.Entities[typeof(WebPushSubscription).FullName!] = await db.WebPushSubscriptions.AsNoTracking()
            .Where(x => demoUsers.Contains(x.UserId)).Select(x => x.Id).ToListAsync(ct);
        // A post-Create update belongs to the showroom only when both its request
        // and its author are recorded in the original manifest. A foreign author
        // on a demo request remains an external reference and blocks Revert.
        var demoRequests = manifest.Entities[typeof(Request).FullName!];
        manifest.Entities[typeof(RequestMessage).FullName!] = manifest.Entities[typeof(RequestMessage).FullName!]
            .Concat(await db.RequestMessages.AsNoTracking()
                .Where(x => demoRequests.Contains(x.RequestId) && demoUsers.Contains(x.AuthorUserId))
                .Select(x => x.Id).ToListAsync(ct)).Distinct().ToList();
        manifest.Entities[typeof(RequestInternalNote).FullName!] = manifest.Entities[typeof(RequestInternalNote).FullName!]
            .Concat(await db.RequestInternalNotes.AsNoTracking()
                .Where(x => demoRequests.Contains(x.RequestId) && demoUsers.Contains(x.AuthorUserId))
                .Select(x => x.Id).ToListAsync(ct)).Distinct().ToList();
        foreach (var (name, ids) in manifest.Entities)
        {
            var type = TypeFor(name);
            var pk = type.FindPrimaryKey();
            if (pk?.Properties.Count != 1 || pk.Properties[0].ClrType != typeof(Guid))
                throw new InvalidOperationException($"Unsupported demo ownership key: {name}.");
            if (ids.Count == 0)
            {
                counts[type.ClrType.Name] = 0;
                continue;
            }
            var existing = await ReadGuidsAsync(
                $"SELECT {Column(type, pk.Properties[0])} FROM {Table(type)} WHERE {Column(type, pk.Properties[0])} IN ({Parameters(ids.Count)})",
                ids, ct);
            counts[type.ClrType.Name] = existing.Count;
        }

        foreach (var (name, ids) in manifest.Entities)
        {
            if (ids.Count == 0) continue;
            var principal = TypeFor(name);
            foreach (var dependent in db.Model.GetEntityTypes())
            foreach (var fk in dependent.GetForeignKeys().Where(x => x.PrincipalEntityType == principal))
            {
                if (fk.Properties.Count != 1 ||
                    (Nullable.GetUnderlyingType(fk.Properties[0].ClrType) ?? fk.Properties[0].ClrType) != typeof(Guid))
                    throw new InvalidOperationException($"Unsupported FK guard: {dependent.ClrType.Name}.");
                var fkColumn = Column(dependent, fk.Properties[0]);
                var pk = dependent.FindPrimaryKey();
                if (pk is null) throw new InvalidOperationException("Entity without key in demo guard.");
                if (dependent.ClrType == typeof(IdentityUserRole<Guid>))
                {
                    var rows = await ReadRoleLinksAsync(
                        $"SELECT \"UserId\", \"RoleId\" FROM {Table(dependent)} WHERE {fkColumn} IN ({Parameters(ids.Count)})",
                        ids, ct);
                    foreach (var row in rows.Where(row => !manifest.Roles.Contains(row)))
                        externalRows.Add($"{dependent.ClrType.FullName}:{row.UserId}:{row.RoleId}");
                    if (rows.Any(row => !manifest.Roles.Contains(row)))
                        conflicts.Add($"{dependent.ClrType.Name}: external role link references demo identity");
                    continue;
                }
                if (pk.Properties.Count != 1 || pk.Properties[0].ClrType != typeof(Guid))
                {
                    var found = await CountAsync(
                        $"SELECT COUNT(*) FROM {Table(dependent)} WHERE {fkColumn} IN ({Parameters(ids.Count)})", ids, ct);
                    if (found > 0) conflicts.Add($"{dependent.ClrType.Name}: untracked dependent rows");
                    for (var index = 0; index < found; index++)
                        externalRows.Add($"{dependent.ClrType.FullName}:unknown:{index}");
                    continue;
                }
                var foundIds = await ReadGuidsAsync(
                    $"SELECT {Column(dependent, pk.Properties[0])} FROM {Table(dependent)} WHERE {fkColumn} IN ({Parameters(ids.Count)})",
                    ids, ct);
                var owned = manifest.Entities.GetValueOrDefault(dependent.ClrType.FullName!) ?? [];
                foreach (var id in foundIds.Where(id => !owned.Contains(id)))
                    externalRows.Add($"{dependent.ClrType.FullName}:{id}");
                if (foundIds.Any(id => !owned.Contains(id)))
                    conflicts.Add($"{dependent.ClrType.Name}: external row references demo data");
            }
            // Several telemetry and context columns intentionally have no database FK.
            // They must still block a destructive cleanup when they point at this dataset.
            foreach (var dependent in db.Model.GetEntityTypes())
            foreach (var property in dependent.GetProperties().Where(x =>
                         (Nullable.GetUnderlyingType(x.ClrType) ?? x.ClrType) == typeof(Guid)
                         && LooksLikeReference(principal, x.Name)
                         && !dependent.GetForeignKeys().Any(fk =>
                             fk.PrincipalEntityType == principal && fk.Properties.Contains(x))))
            {
                var pk = dependent.FindPrimaryKey();
                if (pk?.Properties.Count != 1 || pk.Properties[0].ClrType != typeof(Guid))
                {
                    var found = await CountAsync($"SELECT COUNT(*) FROM {Table(dependent)} WHERE {Column(dependent, property)} IN ({Parameters(ids.Count)})", ids, ct);
                    if (found > 0)
                        conflicts.Add($"{dependent.ClrType.Name}: untracked reference to demo data");
                    for (var index = 0; index < found; index++)
                        externalRows.Add($"{dependent.ClrType.FullName}:unknown:{index}");
                    continue;
                }
                var foundIds = await ReadGuidsAsync(
                    $"SELECT {Column(dependent, pk.Properties[0])} FROM {Table(dependent)} WHERE {Column(dependent, property)} IN ({Parameters(ids.Count)})",
                    ids, ct);
                var owned = manifest.Entities.GetValueOrDefault(dependent.ClrType.FullName!) ?? [];
                foreach (var id in foundIds.Where(id => !owned.Contains(id)))
                    externalRows.Add($"{dependent.ClrType.FullName}:{id}");
                if (foundIds.Any(id => !owned.Contains(id)))
                    conflicts.Add($"{dependent.ClrType.Name}: untracked reference to demo data");
            }
        }
        return new DemoInspection(true, counts, conflicts.Distinct().ToArray(), externalRows.Count);
    }

    private void ValidateManifest(CommercialDemoManifest manifest)
    {
        if (manifest.Entities.Count == 0 ||
            !manifest.Entities.ContainsKey(typeof(Condominium).FullName!) ||
            !manifest.Entities.ContainsKey(typeof(CondominiumDocument).FullName!))
            throw new InvalidOperationException("Commercial demo manifest is incomplete.");
        foreach (var (name, ids) in manifest.Entities)
        {
            _ = TypeFor(name);
            if (ids.Count != ids.Distinct().Count() || ids.Contains(Guid.Empty))
                throw new InvalidOperationException($"Invalid IDs in demo manifest: {name}.");
        }
        if (manifest.Roles.Any(link => !manifest.Entities[typeof(CondoLink.Infrastructure.Identity.ApplicationUser).FullName!]
                .Contains(link.UserId)))
            throw new InvalidOperationException("Role link has no owned user.");
    }

    private IReadOnlyList<IEntityType> DeleteOrder(CommercialDemoManifest manifest)
    {
        var result = new List<IEntityType>();
        var visiting = new HashSet<IEntityType>();
        var visited = new HashSet<IEntityType>();
        var owned = manifest.Entities.Keys.Select(TypeFor).ToHashSet();
        void Visit(IEntityType type)
        {
            if (visited.Contains(type)) return;
            if (!visiting.Add(type)) throw new InvalidOperationException("Demo FK cycle requires manual review.");
            foreach (var child in owned.Where(x => x.GetForeignKeys().Any(fk => fk.PrincipalEntityType == type)))
                Visit(child);
            visiting.Remove(type);
            visited.Add(type);
            result.Add(type);
        }
        foreach (var type in owned) Visit(type);
        return result;
    }

    private IEntityType TypeFor(string name) => db.Model.GetEntityTypes()
        .SingleOrDefault(x => x.ClrType.FullName == name)
        ?? throw new InvalidOperationException($"Unknown demo entity type: {name}.");

    private static string Table(IEntityType type) => type.GetSchema() is { } schema
        ? $"\"{schema}\".\"{type.GetTableName()}\""
        : $"\"{type.GetTableName()}\"";

    private static string Column(IEntityType type, IProperty property) =>
        $"\"{property.GetColumnName(StoreObjectIdentifier.Table(type.GetTableName()!, type.GetSchema()))}\"";

    private static string Parameters(int count) =>
        string.Join(", ", Enumerable.Range(0, count).Select(x => $"@p{x}"));

    private static bool LooksLikeReference(IEntityType principal, string propertyName) =>
        principal.ClrType.Name switch
        {
            "Condominium" => propertyName.EndsWith("CondominiumId", StringComparison.Ordinal),
            "ApplicationUser" => propertyName.EndsWith("UserId", StringComparison.Ordinal),
            "Request" or "ManagementCompanyRequest" => propertyName.EndsWith("RequestId", StringComparison.Ordinal),
            "ManagementCompany" => propertyName.EndsWith("ManagementCompanyId", StringComparison.Ordinal),
            "ServiceProvider" => propertyName.EndsWith("ServiceProviderId", StringComparison.Ordinal),
            _ => propertyName == principal.ClrType.Name + "Id"
        };

    private async Task<DbCommand> CommandAsync(string sql, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        for (var index = 0; index < ids.Count; index++)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = $"@p{index}";
            parameter.DbType = DbType.Guid;
            parameter.Value = ids[index];
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private async Task<List<Guid>> ReadGuidsAsync(string sql, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await using var command = await CommandAsync(sql, ids, ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<Guid>();
        while (await reader.ReadAsync(ct)) result.Add(ReadGuid(reader, 0));
        return result;
    }

    private async Task<List<DemoRoleLink>> ReadRoleLinksAsync(string sql, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await using var command = await CommandAsync(sql, ids, ct);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var result = new List<DemoRoleLink>();
        while (await reader.ReadAsync(ct)) result.Add(new DemoRoleLink(ReadGuid(reader, 0), ReadGuid(reader, 1)));
        return result;
    }

    private async Task<long> CountAsync(string sql, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await using var command = await CommandAsync(sql, ids, ct);
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private async Task ExecuteAsync(string sql, IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        await using var command = await CommandAsync(sql, ids, ct);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static Guid ReadGuid(DbDataReader reader, int ordinal) => reader.GetValue(ordinal) is Guid id
        ? id : Guid.Parse(reader.GetString(ordinal));
}

internal sealed record DemoInspection(bool Exists, Dictionary<string, int> Counts,
    IReadOnlyList<string> Conflicts, int ExternalRecordsAffected);
