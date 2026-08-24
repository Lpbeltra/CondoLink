using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalAgenda : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agenda_reminders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_third_party = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    starts_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    next_occurrence_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    recurrence_type = table.Column<int>(type: "integer", nullable: false),
                    recurrence_day_of_month = table.Column<int>(type: "integer", nullable: false),
                    notify_by_whatsapp = table.Column<bool>(type: "boolean", nullable: false),
                    notify_by_email = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_reminders", x => x.id);
                    table.ForeignKey(
                        name: "FK_agenda_reminders_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agenda_reminders_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "agenda_reminder_occurrences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reminder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_for_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    email_status = table.Column<int>(type: "integer", nullable: false),
                    email_attempts = table.Column<int>(type: "integer", nullable: false),
                    email_diagnostic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    whatsapp_status = table.Column<int>(type: "integer", nullable: false),
                    whatsapp_attempts = table.Column<int>(type: "integer", nullable: false),
                    whatsapp_diagnostic = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    whatsapp_outbound_message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    processed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_reminder_occurrences", x => x.id);
                    table.ForeignKey(
                        name: "FK_agenda_reminder_occurrences_agenda_reminders_reminder_id",
                        column: x => x.reminder_id,
                        principalTable: "agenda_reminders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agenda_reminder_occurrences_whatsapp_outbound_messages_what~",
                        column: x => x.whatsapp_outbound_message_id,
                        principalTable: "whatsapp_outbound_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "agenda_reminder_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reminder_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agenda_reminder_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_agenda_reminder_requests_agenda_reminders_reminder_id",
                        column: x => x.reminder_id,
                        principalTable: "agenda_reminders",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agenda_reminder_requests_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agenda_reminder_occurrences_reminder_id_scheduled_for_utc",
                table: "agenda_reminder_occurrences",
                columns: new[] { "reminder_id", "scheduled_for_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agenda_reminder_occurrences_whatsapp_outbound_message_id",
                table: "agenda_reminder_occurrences",
                column: "whatsapp_outbound_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_agenda_reminder_requests_reminder_id",
                table: "agenda_reminder_requests",
                column: "reminder_id");

            migrationBuilder.CreateIndex(
                name: "IX_agenda_reminder_requests_request_id",
                table: "agenda_reminder_requests",
                column: "request_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agenda_reminders_condominium_id_is_active_next_occurrence_a~",
                table: "agenda_reminders",
                columns: new[] { "condominium_id", "is_active", "next_occurrence_at_utc" });

            migrationBuilder.CreateIndex(
                name: "IX_agenda_reminders_unit_id",
                table: "agenda_reminders",
                column: "unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agenda_reminder_occurrences");

            migrationBuilder.DropTable(
                name: "agenda_reminder_requests");

            migrationBuilder.DropTable(
                name: "agenda_reminders");
        }
    }
}
