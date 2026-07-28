using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "whatsapp_inbound_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    provider_timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    identified_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    processing_result = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_inbound_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_whatsapp_inbound_messages_users_identified_user_id",
                        column: x => x.identified_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "whatsapp_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    previous_state = table.Column<int>(type: "integer", nullable: true),
                    last_interaction_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_whatsapp_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_whatsapp_sessions_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_whatsapp_sessions_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_whatsapp_sessions_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_whatsapp_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_inbound_messages_identified_user_id",
                table: "whatsapp_inbound_messages",
                column: "identified_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_whatsapp_inbound_messages_external_id",
                table: "whatsapp_inbound_messages",
                column: "external_message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_sessions_condominium_id",
                table: "whatsapp_sessions",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_sessions_request_id",
                table: "whatsapp_sessions",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_sessions_unit_id",
                table: "whatsapp_sessions",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_sessions_user_id",
                table: "whatsapp_sessions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_whatsapp_sessions_phone_number",
                table: "whatsapp_sessions",
                column: "phone_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "whatsapp_inbound_messages");

            migrationBuilder.DropTable(
                name: "whatsapp_sessions");
        }
    }
}
