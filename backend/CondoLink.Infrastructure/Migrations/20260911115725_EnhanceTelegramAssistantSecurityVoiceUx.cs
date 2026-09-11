using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceTelegramAssistantSecurityVoiceUx : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PendingTelegramChatId",
                table: "telegram_link_codes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PendingTelegramUserId",
                table: "telegram_link_codes",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerificationStartedAt",
                table: "telegram_link_codes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AssistantDurationMs",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AssistantExecutionId",
                table: "telegram_inbound_updates",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AudioDownloadDurationMs",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "AuthorizationDurationMs",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhoneNumber",
                table: "telegram_inbound_updates",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ContactUserId",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "DeliveryDurationMs",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationSeconds",
                table: "telegram_inbound_updates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileId",
                table: "telegram_inbound_updates",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                table: "telegram_inbound_updates",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSize",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "telegram_inbound_updates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                table: "telegram_inbound_updates",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "QueueDurationMs",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReplyMarkup",
                table: "telegram_inbound_updates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<long>(
                name: "TotalDurationMs",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TranscriptionDurationMs",
                table: "telegram_inbound_updates",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_link_codes_PendingTelegramUserId_PendingTelegramCh~",
                table: "telegram_link_codes",
                columns: new[] { "PendingTelegramUserId", "PendingTelegramChatId", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_telegram_link_codes_PendingTelegramUserId_PendingTelegramCh~",
                table: "telegram_link_codes");

            migrationBuilder.DropColumn(
                name: "PendingTelegramChatId",
                table: "telegram_link_codes");

            migrationBuilder.DropColumn(
                name: "PendingTelegramUserId",
                table: "telegram_link_codes");

            migrationBuilder.DropColumn(
                name: "VerificationStartedAt",
                table: "telegram_link_codes");

            migrationBuilder.DropColumn(
                name: "AssistantDurationMs",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "AssistantExecutionId",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "AudioDownloadDurationMs",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "AuthorizationDurationMs",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "ContactPhoneNumber",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "ContactUserId",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "DeliveryDurationMs",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "FileId",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "FileName",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "FileSize",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "MimeType",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "QueueDurationMs",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "ReplyMarkup",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "TotalDurationMs",
                table: "telegram_inbound_updates");

            migrationBuilder.DropColumn(
                name: "TranscriptionDurationMs",
                table: "telegram_inbound_updates");
        }
    }
}
