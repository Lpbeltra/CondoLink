using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260730120000_AddNormalizedUserPhoneNumber")]
public partial class AddNormalizedUserPhoneNumber : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "normalized_phone_number",
            table: "users",
            type: "character varying(14)",
            maxLength: 14,
            nullable: true);

        // Invalid/empty legacy values remain null. A canonical value is assigned
        // only when exactly one user owns it; every member of a duplicate group
        // remains unidentified until the records are corrected explicitly.
        migrationBuilder.Sql(
            """
            WITH digits AS (
                SELECT id,
                       regexp_replace(phone_number, '[^0-9]', '', 'g') AS value
                FROM users
                WHERE phone_number IS NOT NULL
                  AND btrim(phone_number) <> ''
            ),
            international_prefix AS (
                SELECT id,
                       CASE
                           WHEN value LIKE '00%'
                               THEN CASE
                                   WHEN length(value) >= 6
                                        AND substring(value FROM 5 FOR 2) = '55'
                                       THEN substring(value FROM 5)
                                   ELSE substring(value FROM 3)
                               END
                           ELSE value
                       END AS value
                FROM digits
            ),
            country_code AS (
                SELECT id,
                       CASE WHEN length(value) IN (10, 11)
                           THEN '55' || value ELSE value END AS value
                FROM international_prefix
            ),
            valid AS (
                SELECT id, '+' || value AS canonical
                FROM country_code
                WHERE value LIKE '55%'
                  AND length(value) IN (12, 13)
            ),
            unique_values AS (
                SELECT canonical
                FROM valid
                GROUP BY canonical
                HAVING count(*) = 1
            )
            UPDATE users AS target
            SET normalized_phone_number = valid.canonical
            FROM valid
            INNER JOIN unique_values
                ON unique_values.canonical = valid.canonical
            WHERE target.id = valid.id;
            """);

        migrationBuilder.CreateIndex(
            name: "ux_users_normalized_phone_number",
            table: "users",
            column: "normalized_phone_number",
            unique: true,
            filter: "\"normalized_phone_number\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_users_normalized_phone_number",
            table: "users");

        migrationBuilder.DropColumn(
            name: "normalized_phone_number",
            table: "users");
    }
}
