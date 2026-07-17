using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCondominiumBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_units_condominium_block_identifier",
                table: "units");

            migrationBuilder.DropIndex(
                name: "ux_units_condominium_identifier_without_block",
                table: "units");

            migrationBuilder.AddColumn<Guid>(
                name: "block_id",
                table: "units",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "condominium_blocks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    identifier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condominium_blocks", x => x.id);
                    table.ForeignKey(
                        name: "FK_condominium_blocks_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO condominium_blocks (id, condominium_id, identifier, created_at, updated_at)
                SELECT gen_random_uuid(), condominium_id, MIN(BTRIM(block)), NOW(), NOW()
                FROM units
                WHERE block IS NOT NULL AND BTRIM(block) <> ''
                GROUP BY condominium_id, LOWER(BTRIM(block));

                UPDATE units AS unit
                SET block_id = block.id
                FROM condominium_blocks AS block
                WHERE block.condominium_id = unit.condominium_id
                  AND LOWER(block.identifier) = LOWER(BTRIM(unit.block));
                """);

            migrationBuilder.DropColumn(
                name: "block",
                table: "units");

            migrationBuilder.CreateIndex(
                name: "ux_units_block_identifier",
                table: "units",
                columns: new[] { "block_id", "identifier" },
                unique: true,
                filter: "block_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_units_condominium_identifier_without_block_id",
                table: "units",
                columns: new[] { "condominium_id", "identifier" },
                unique: true,
                filter: "block_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_condominium_blocks_condominium_identifier",
                table: "condominium_blocks",
                columns: new[] { "condominium_id", "identifier" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_units_condominium_blocks_block_id",
                table: "units",
                column: "block_id",
                principalTable: "condominium_blocks",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_units_condominium_blocks_block_id",
                table: "units");

            migrationBuilder.DropIndex(
                name: "ux_units_block_identifier",
                table: "units");

            migrationBuilder.DropIndex(
                name: "ux_units_condominium_identifier_without_block_id",
                table: "units");

            migrationBuilder.AddColumn<string>(
                name: "block",
                table: "units",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE units AS unit
                SET block = block.identifier
                FROM condominium_blocks AS block
                WHERE block.id = unit.block_id;
                """);

            migrationBuilder.DropColumn(
                name: "block_id",
                table: "units");

            migrationBuilder.DropTable(
                name: "condominium_blocks");

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
    }
}
