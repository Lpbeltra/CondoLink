using System.Reflection;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CondoLink.Tests;

public sealed class RestoreGlobalSubManagerUniquenessMigrationTests
{
    [Fact]
    public void AppDbContext_discovers_new_and_previous_migrations()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused")
            .Options;
        using var db = new AppDbContext(options);
        var migrations = db.GetService<IMigrationsAssembly>().Migrations;

        Assert.Contains("20260903090000_RestoreGlobalSubManagerUniqueness", migrations.Keys);
        Assert.Contains("20260902175312_IncreaseRequestConversationMessageLimitV2", migrations.Keys);
    }

    [Fact]
    public void Migration_sql_preserves_global_user_only_rule()
    {
        var type = typeof(AppDbContext).Assembly
            .GetTypes().Single(x => x.Name == "RestoreGlobalSubManagerUniqueness");
        var migration = (Migration)Activator.CreateInstance(type)!;
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
