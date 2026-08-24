using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorrelateWhatsAppOperationalReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "request_closure_confirmation_id",
                table: "whatsapp_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "request_closure_confirmation_id",
                table: "whatsapp_outbound_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "request_status_history_id",
                table: "whatsapp_outbound_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_sessions_request_closure_confirmation_id",
                table: "whatsapp_sessions",
                column: "request_closure_confirmation_id");

            migrationBuilder.AddForeignKey(
                name: "FK_whatsapp_sessions_request_closure_confirmations_request_clo~",
                table: "whatsapp_sessions",
                column: "request_closure_confirmation_id",
                principalTable: "request_closure_confirmations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_whatsapp_sessions_request_closure_confirmations_request_clo~",
                table: "whatsapp_sessions");

            migrationBuilder.DropIndex(
                name: "IX_whatsapp_sessions_request_closure_confirmation_id",
                table: "whatsapp_sessions");

            migrationBuilder.DropColumn(
                name: "request_closure_confirmation_id",
                table: "whatsapp_sessions");

            migrationBuilder.DropColumn(
                name: "request_closure_confirmation_id",
                table: "whatsapp_outbound_messages");

            migrationBuilder.DropColumn(
                name: "request_status_history_id",
                table: "whatsapp_outbound_messages");
        }
    }
}
