using CondoLink.Infrastructure.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace CondoLink.Tests;

public sealed class NormalizedPhoneMigrationTests
{
    [Fact]
    public void Backfill_keeps_invalid_and_duplicate_legacy_phones_unidentified()
    {
        var builder = new MigrationBuilder(
            "Npgsql.EntityFrameworkCore.PostgreSQL");

        new TestableMigration().BuildUp(builder);

        var sql = Assert.Single(builder.Operations.OfType<SqlOperation>()).Sql;
        Assert.Contains("btrim(phone_number) <> ''", sql);
        Assert.Contains("length(value) IN (12, 13)", sql);
        Assert.Contains("HAVING count(*) = 1", sql);
        Assert.Contains(
            "SET normalized_phone_number = valid.canonical",
            sql);
    }

    private sealed class TestableMigration
        : AddNormalizedUserPhoneNumber
    {
        public void BuildUp(MigrationBuilder migrationBuilder) =>
            Up(migrationBuilder);
    }
}
