using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCondominiumDocumentKnowledge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "condominium_document_knowledge",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    topics_json = table.Column<string>(type: "jsonb", nullable: false),
                    entities_json = table.Column<string>(type: "jsonb", nullable: false),
                    dates_json = table.Column<string>(type: "jsonb", nullable: false),
                    facts_json = table.Column<string>(type: "jsonb", nullable: false),
                    search_text = table.Column<string>(type: "text", nullable: false),
                    analyzer_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condominium_document_knowledge", x => x.id);
                    table.ForeignKey(
                        name: "FK_condominium_document_knowledge_condominium_documents_condom~",
                        column: x => x.condominium_document_id,
                        principalTable: "condominium_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_condominium_document_knowledge_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_condominium_document_knowledge_condominium_document_id",
                table: "condominium_document_knowledge",
                column: "condominium_document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_condominium_document_knowledge_condominium_id",
                table: "condominium_document_knowledge",
                column: "condominium_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "condominium_document_knowledge");
        }
    }
}
