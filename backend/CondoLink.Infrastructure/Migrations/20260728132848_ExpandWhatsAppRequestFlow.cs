using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandWhatsAppRequestFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "whatsapp_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "draft_description",
                table: "whatsapp_sessions",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "page",
                table: "whatsapp_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "source",
                table: "requests",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "channel",
                table: "request_messages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "whatsapp_draft_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_media_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_draft_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_whatsapp_draft_attachments_whatsapp_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "whatsapp_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_sessions_category_id",
                table: "whatsapp_sessions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_draft_attachments_session_id_created_at",
                table: "whatsapp_draft_attachments",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_whatsapp_draft_attachments_external_media_id",
                table: "whatsapp_draft_attachments",
                column: "external_media_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_whatsapp_sessions_categories_category_id",
                table: "whatsapp_sessions",
                column: "category_id",
                principalTable: "categories",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_whatsapp_sessions_categories_category_id",
                table: "whatsapp_sessions");

            migrationBuilder.DropTable(
                name: "whatsapp_draft_attachments");

            migrationBuilder.DropIndex(
                name: "IX_whatsapp_sessions_category_id",
                table: "whatsapp_sessions");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "whatsapp_sessions");

            migrationBuilder.DropColumn(
                name: "draft_description",
                table: "whatsapp_sessions");

            migrationBuilder.DropColumn(
                name: "page",
                table: "whatsapp_sessions");

            migrationBuilder.DropColumn(
                name: "source",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "channel",
                table: "request_messages");
        }
    }
}
