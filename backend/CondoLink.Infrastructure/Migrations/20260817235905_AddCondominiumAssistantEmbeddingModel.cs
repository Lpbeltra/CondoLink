using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCondominiumAssistantEmbeddingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "embedding_model",
                table: "condominium_document_chunks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "local-feature-hash-v1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "embedding_model",
                table: "condominium_document_chunks");
        }
    }
}
