using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncCondominiumModuleSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "condominium_modules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CondominiumId = table.Column<Guid>(type: "uuid", nullable: false),
                    Module = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ManagementCompanyAccessEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condominium_modules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_condominium_modules_condominiums_CondominiumId",
                        column: x => x.CondominiumId,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_condominium_modules_CondominiumId_Module",
                table: "condominium_modules",
                columns: new[] { "CondominiumId", "Module" },
                unique: true);

            // Preserve all currently available capabilities for existing condominiums.
            // EmployeeManagement is reserved for a future product and begins disabled.
            //
            // condominium_modules has no explicit HasColumnName mappings in
            // CondominiumModuleConfiguration (unlike every other table in this
            // schema), so EF/Npgsql fell back to the raw CLR property names as
            // column identifiers — quoted, case-preserved: "Id", "CondominiumId",
            // "Module", "IsEnabled", "ManagementCompanyAccessEnabled", "CreatedAt",
            // "UpdatedAt". This INSERT must target those exact identifiers, not the
            // snake_case convention used elsewhere in the codebase.
            migrationBuilder.Sql("""
                INSERT INTO condominium_modules
                    ("Id", "CondominiumId", "Module", "IsEnabled", "ManagementCompanyAccessEnabled", "CreatedAt", "UpdatedAt")
                SELECT gen_random_uuid(), c.id, m.module, m.module <> 5, FALSE, NOW(), NOW()
                FROM condominiums c
                CROSS JOIN (VALUES (1), (2), (3), (4), (5)) AS m(module)
                ON CONFLICT ("CondominiumId", "Module") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condominium_modules");
        }
    }
}
