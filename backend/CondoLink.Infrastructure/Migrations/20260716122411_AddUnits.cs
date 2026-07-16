using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    block = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    floor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_units", x => x.id);
                    table.ForeignKey(
                        name: "FK_units_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_units_condominium_block_identifier",
                table: "units",
                columns: new[] { "condominium_id", "block", "identifier" },
                unique: true,
                filter: "block IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_units_condominium_identifier_without_block",
                table: "units",
                columns: new[] { "condominium_id", "identifier" },
                unique: true,
                filter: "block IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "units");
        }
    }
}
