using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppOutboundNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "receive_whatsapp_updates",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "whatsapp_display_name",
                table: "condominiums",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "whatsapp_updates_enabled",
                table: "condominiums",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "whatsapp_outbound_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    destination_phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notification_type = table.Column<int>(type: "integer", nullable: false),
                    send_mode = table.Column<int>(type: "integer", nullable: false),
                    template_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    template_language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    content = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    external_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    manual_retry_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    read_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_error_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_error_description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_outbound_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_whatsapp_outbound_messages_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_outbound_messages_request_messages_request_message~",
                        column: x => x.request_message_id,
                        principalTable: "request_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_outbound_messages_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_whatsapp_outbound_messages_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbound_messages_condominium_id_created_at",
                table: "whatsapp_outbound_messages",
                columns: new[] { "condominium_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbound_messages_request_id",
                table: "whatsapp_outbound_messages",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbound_messages_request_message_id",
                table: "whatsapp_outbound_messages",
                column: "request_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbound_messages_status_next_attempt_at",
                table: "whatsapp_outbound_messages",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbound_messages_user_id",
                table: "whatsapp_outbound_messages",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_whatsapp_outbound_external_message_id",
                table: "whatsapp_outbound_messages",
                column: "external_message_id",
                unique: true,
                filter: "\"external_message_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_whatsapp_outbound_idempotency_key",
                table: "whatsapp_outbound_messages",
                column: "idempotency_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_outbound_messages");

            migrationBuilder.DropColumn(
                name: "receive_whatsapp_updates",
                table: "users");

            migrationBuilder.DropColumn(
                name: "whatsapp_display_name",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "whatsapp_updates_enabled",
                table: "condominiums");
        }
    }
}
