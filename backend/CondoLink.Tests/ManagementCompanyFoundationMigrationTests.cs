using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using CondoLink.Infrastructure.Persistence;

namespace CondoLink.Tests;

public sealed class ManagementCompanyFoundationMigrationTests
{
    [Fact]
    public void Migration_preserves_existing_links_seeds_categories_and_enforces_submanager_uniqueness()
    {
        var type = typeof(AppDbContext).Assembly
            .GetTypes().Single(x => x.Name == "AddManagementCompanyFoundation");
        var migration = (Migration)Activator.CreateInstance(type)!;
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        type.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(migration, [builder]);
        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("INSERT INTO condominium_management_company_links", sql);
        Assert.Contains("Solicitação de pagamento", sql);
        Assert.Contains("enforce_single_active_submanager_role", sql);
        Assert.Contains("pg_advisory_xact_lock", sql);
    }
}
