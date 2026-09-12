using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace CondoLink.Tests;

/// <summary>
/// Regression coverage for the production incident where
/// <c>20260911181129_SyncCondominiumModuleSnapshot</c> failed on deploy with
/// Postgres error 42703 ("column \"id\" of relation \"condominium_modules\"
/// does not exist"): <c>CondominiumModuleConfiguration</c> has no
/// <c>HasColumnName</c> mappings, so EF/Npgsql created the table with quoted,
/// case-preserved columns ("Id", "CondominiumId", ...) while the migration's
/// hand-written bootstrap INSERT assumed this codebase's usual snake_case
/// convention (id, condominium_id, ...) — a mismatch that only a real
/// Postgres run surfaces (Sqlite/InMemory happily accept either casing).
///
/// This test recreates the exact failure conditions against a real Postgres
/// database: migrate to the commit immediately before the broken migration
/// (reproducing the legacy schema every existing production database is
/// actually running), seed condominiums the way production has them, then
/// migrate forward through every migration up to the current tip — including
/// the two newer batches (AddEmployeeManagement, AddEmployeeDocumentsAndDelivery)
/// that must keep working unmodified.
///
/// Skipped when COMVY_TEST_POSTGRES is not configured (no Postgres available in
/// this environment/CI) — the same convention every other Postgres-only test in
/// this suite already follows. That is a real limitation: without a Postgres
/// target, this exact class of bug (correct on Sqlite, broken on Postgres) is
/// invisible to `dotnet test`.
/// </summary>
public sealed class CondominiumModuleSnapshotMigrationTests
{
    private const string LegacyTargetMigration = "20260911172414_AddAssistantSecondPassMetrics";

    private static string? BaseConnection => Environment.GetEnvironmentVariable("COMVY_TEST_POSTGRES");

    [Fact]
    public async Task Legacy_schema_upgrades_through_the_fixed_bootstrap_and_later_batches_with_correct_defaults()
    {
        if (BaseConnection is null) return;

        var scratchDatabase = $"condolink_migration_repro_{Guid.NewGuid():N}";
        var scratchConnectionString = new NpgsqlConnectionStringBuilder(BaseConnection) { Database = scratchDatabase }.ToString();
        await using (var maintenance = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(BaseConnection) { Database = "postgres" }.ToString()))
        {
            await maintenance.OpenAsync();
            await using var create = maintenance.CreateCommand();
            create.CommandText = $"CREATE DATABASE \"{scratchDatabase}\"";
            await create.ExecuteNonQueryAsync();
        }

        try
        {
            var condominiumAId = Guid.NewGuid();
            var condominiumBId = Guid.NewGuid();

            // 1. Reproduce the legacy schema every existing production database
            // is actually running today — one migration short of the broken one.
            await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scratchConnectionString).Options))
            {
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync(LegacyTargetMigration);

                Assert.False(await TableExistsAsync(db, "condominium_modules"));

                // 2. Seed condominiums the way production has them: created before
                // this migration ever ran, with no condominium_modules rows yet.
                await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO condominiums (id, name, is_active, created_at, updated_at, has_doorman, is_remote_doorman, whatsapp_updates_enabled)
                    VALUES
                        ({condominiumAId}, 'Condomínio Legado A', true, now(), now(), false, false, true),
                        ({condominiumBId}, 'Condomínio Legado B', true, now(), now(), false, false, true)
                    """);
            }

            // 3. Migrate forward through the fixed bootstrap AND every later
            // batch (EmployeeManagement, EmployeeDocuments/Delivery) in one go —
            // proving the whole chain, not just the one migration in isolation.
            await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scratchConnectionString).Options))
            {
                var migrator = db.GetService<IMigrator>();
                await migrator.MigrateAsync();
            }

            // 4. Assert the bootstrap produced exactly the expected rows: every
            // pre-existing module enabled, EmployeeManagement disabled, no
            // management-company delegation turned on by surprise.
            await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scratchConnectionString).Options))
            {
                var rows = await db.CondominiumModules.AsNoTracking()
                    .Where(x => x.CondominiumId == condominiumAId || x.CondominiumId == condominiumBId)
                    .ToListAsync();
                Assert.Equal(10, rows.Count); // 5 modules × 2 condominiums
                foreach (var condominiumId in new[] { condominiumAId, condominiumBId })
                {
                    var forCondominium = rows.Where(x => x.CondominiumId == condominiumId).ToArray();
                    Assert.Equal(5, forCondominium.Length);
                    Assert.All(forCondominium, row => Assert.False(row.ManagementCompanyAccessEnabled));
                    Assert.True(forCondominium.Where(x => x.Module != CondoLink.Domain.Enums.CondominiumModuleType.EmployeeManagement).All(x => x.IsEnabled));
                    Assert.False(forCondominium.Single(x => x.Module == CondoLink.Domain.Enums.CondominiumModuleType.EmployeeManagement).IsEnabled);
                }
            }

            // 5. Idempotency: a manually-customized row must never be reintroduced
            // or overwritten if the bootstrap statement ever ran again (e.g. a
            // future migration reusing the same pattern against this table).
            await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(scratchConnectionString).Options))
            {
                var customized = await db.CondominiumModules
                    .SingleAsync(x => x.CondominiumId == condominiumAId && x.Module == CondoLink.Domain.Enums.CondominiumModuleType.Documents);
                customized.Set(false, false, DateTime.UtcNow); // an operator manually disabled this module
                await db.SaveChangesAsync();

                await db.Database.ExecuteSqlRawAsync("""
                    INSERT INTO condominium_modules
                        ("Id", "CondominiumId", "Module", "IsEnabled", "ManagementCompanyAccessEnabled", "CreatedAt", "UpdatedAt")
                    SELECT gen_random_uuid(), c.id, m.module, m.module <> 5, FALSE, NOW(), NOW()
                    FROM condominiums c
                    CROSS JOIN (VALUES (1), (2), (3), (4), (5)) AS m(module)
                    ON CONFLICT ("CondominiumId", "Module") DO NOTHING;
                    """);

                var totalRows = await db.CondominiumModules.CountAsync();
                Assert.Equal(10, totalRows); // unchanged — no duplicates
                var stillDisabled = await db.CondominiumModules.AsNoTracking()
                    .SingleAsync(x => x.CondominiumId == condominiumAId && x.Module == CondoLink.Domain.Enums.CondominiumModuleType.Documents);
                Assert.False(stillDisabled.IsEnabled); // the manual customization survived
            }
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var maintenance = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(BaseConnection) { Database = "postgres" }.ToString());
            await maintenance.OpenAsync();
            await using var drop = maintenance.CreateCommand();
            drop.CommandText = $"DROP DATABASE IF EXISTS \"{scratchDatabase}\" WITH (FORCE)";
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<bool> TableExistsAsync(AppDbContext db, string tableName)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT to_regclass(@table) IS NOT NULL";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "table";
        parameter.Value = tableName;
        command.Parameters.Add(parameter);
        if (command.Connection!.State != System.Data.ConnectionState.Open) await db.Database.OpenConnectionAsync();
        var result = await command.ExecuteScalarAsync();
        return result is true;
    }
}
