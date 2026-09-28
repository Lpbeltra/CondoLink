using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalCondominiumMappings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "external_condominium_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    administrator_integration_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_condominium_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_condominium_mappings", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_condominium_mappings_administrator_integrations_ad~",
                        column: x => x.administrator_integration_id,
                        principalTable: "administrator_integrations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_external_condominium_mappings_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_external_condominium_mappings_condominium_id",
                table: "external_condominium_mappings",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "ux_external_condominium_mappings_integration_condominium",
                table: "external_condominium_mappings",
                columns: new[] { "administrator_integration_id", "condominium_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_external_condominium_mappings_integration_external_id",
                table: "external_condominium_mappings",
                columns: new[] { "administrator_integration_id", "external_condominium_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "external_condominium_mappings");
        }
    }
}
