using CondoLink.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CondoLink.Tests;

public sealed class WhatsAppUpdatesDefaultMigrationTests
{
    [Fact]
    public void Migration_changes_default_and_backfills_existing_condominiums()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        new TestableMigration().BuildUp(builder);

        var alter = Assert.Single(builder.Operations.OfType<AlterColumnOperation>());
        Assert.Equal("whatsapp_updates_enabled", alter.Name);
        Assert.Equal(true, alter.DefaultValue);
        var sql = Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
        Assert.Contains("SET whatsapp_updates_enabled = TRUE", sql);
        Assert.Contains("WHERE whatsapp_updates_enabled = FALSE", sql);
    }

    private sealed class TestableMigration : EnableWhatsAppUpdatesByDefault
    {
        public void BuildUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
