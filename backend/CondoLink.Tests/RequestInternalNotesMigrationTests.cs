using CondoLink.Infrastructure.Migrations;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CondoLink.Tests;

public sealed class RequestInternalNotesMigrationTests
{
    [Fact]
    public void Migration_is_additive_and_generates_postgresql_sql_with_a_scoped_rollback()
    {
        var migration = new AddRequestInternalNotes { ActiveProvider = "Npgsql.EntityFrameworkCore.PostgreSQL" };
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("request_internal_notes", table.Name);
        Assert.All(migration.UpOperations, operation => Assert.True(operation is CreateTableOperation or CreateIndexOperation));
        Assert.All(table.ForeignKeys, key => Assert.Equal(ReferentialAction.Restrict, key.OnDelete));
        Assert.Equal("request_internal_notes", Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations)).Name);

        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=migration_script_only;Username=unused;Password=unused").Options);
        var sql = db.GetService<IMigrator>().GenerateScript("20260903090000_RestoreGlobalSubManagerUniqueness",
            "20260907143425_AddRequestInternalNotes", MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE request_internal_notes", sql);
        Assert.Contains("timestamp with time zone", sql);
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain("UPDATE requests", sql);
    }
}
