using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Migrations;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CondoLink.Tests;

public sealed class UserWhatsAppUpdatesDefaultMigrationTests
{
    [Fact]
    public async Task Ef_default_is_true()
    {
        await using var host = await CoreEndpointTestHost.StartAsync(_ => { });
        await host.WithDbAsync(db =>
        {
            var property = db.Model.FindEntityType(typeof(ApplicationUser))!
                .FindProperty(nameof(ApplicationUser.ReceiveWhatsAppUpdates))!;
            Assert.Equal(true, property.GetDefaultValue());
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void Migration_changes_default_and_backfills_false_users()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new TestableMigration().BuildUp(builder);

        var alter = Assert.Single(builder.Operations.OfType<AlterColumnOperation>());
        Assert.Equal("receive_whatsapp_updates", alter.Name);
        Assert.Equal(true, alter.DefaultValue);
        var sql = Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
        Assert.Contains("SET receive_whatsapp_updates = TRUE", sql);
        Assert.Contains("WHERE receive_whatsapp_updates = FALSE", sql);
    }

    private sealed class TestableMigration : EnableUserWhatsAppUpdatesByDefault
    {
        public void BuildUp(MigrationBuilder migrationBuilder) => Up(migrationBuilder);
    }
}
