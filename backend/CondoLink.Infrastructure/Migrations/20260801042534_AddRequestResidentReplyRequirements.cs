using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestResidentReplyRequirements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_resident_reply_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_status_history_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    answered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    answer_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    has_unread_answer = table.Column<bool>(type: "boolean", nullable: false),
                    reminder_count = table.Column<int>(type: "integer", nullable: false),
                    last_reminder_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_resident_reply_requirements", x => x.id);
                    table.ForeignKey(
                        name: "FK_request_resident_reply_requirements_request_messages_answer~",
                        column: x => x.answer_message_id,
                        principalTable: "request_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_request_resident_reply_requirements_request_status_history_~",
                        column: x => x.request_status_history_id,
                        principalTable: "request_status_history",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_request_resident_reply_requirements_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_request_resident_reply_requirements_users_requested_by_user~",
                        column: x => x.requested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_request_resident_reply_requirements_answer_message_id",
                table: "request_resident_reply_requirements",
                column: "answer_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_resident_reply_requirements_request_id",
                table: "request_resident_reply_requirements",
                column: "request_id",
                unique: true,
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_request_resident_reply_requirements_request_id_requested_at",
                table: "request_resident_reply_requirements",
                columns: new[] { "request_id", "requested_at" });

            migrationBuilder.CreateIndex(
                name: "IX_request_resident_reply_requirements_request_status_history_~",
                table: "request_resident_reply_requirements",
                column: "request_status_history_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_resident_reply_requirements_requested_by_user_id",
                table: "request_resident_reply_requirements",
                column: "requested_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_resident_reply_requirements");
        }
    }
}
