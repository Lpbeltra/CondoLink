using System.Reflection;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CondoLink.Tests;

public sealed class RestoreGlobalSubManagerUniquenessMigrationTests
{
    [Fact]
    public void Recreates_trigger_with_global_user_only_rule()
    {
        var type = typeof(AppDbContext).Assembly
            .GetTypes().Single(x => x.Name == "RestoreGlobalSubManagerUniqueness");
        var migration = (Migration)Activator.CreateInstance(type)!;
        Assert.Equal(
            "20260903090000_RestoreGlobalSubManagerUniqueness",
            type.GetCustomAttribute<MigrationAttribute>()?.Id);
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        type.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));

        Assert.Contains("CREATE OR REPLACE FUNCTION enforce_single_active_submanager_role", sql);
        Assert.Contains("m.user_id = v_user", sql);
        Assert.Contains("pg_advisory_xact_lock(hashtextextended(v_user::text, 9182))", sql);
        Assert.DoesNotContain("v_condominium", sql);
        Assert.DoesNotContain("m.condominium_id =", sql);
        Assert.Contains("CREATE TRIGGER trg_single_active_submanager_role", sql);
    }
}
