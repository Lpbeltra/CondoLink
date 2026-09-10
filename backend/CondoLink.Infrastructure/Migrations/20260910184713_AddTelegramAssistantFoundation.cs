using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTelegramAssistantFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "channel",
                table: "condominium_assistant_conversations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Channel",
                table: "assistant_execution_metrics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "telegram_inbound_updates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdateId = table.Column<long>(type: "bigint", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Text = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessingStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ResponseText = table.Column<string>(type: "text", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    UserMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    SentPartCount = table.Column<int>(type: "integer", nullable: false),
                    ProcessingToken = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_inbound_updates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "telegram_link_codes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvalidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_link_codes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telegram_link_codes_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "telegram_user_links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TelegramUserId = table.Column<long>(type: "bigint", nullable: false),
                    TelegramChatId = table.Column<long>(type: "bigint", nullable: false),
                    ActiveCondominiumId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_telegram_user_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_telegram_user_links_condominiums_ActiveCondominiumId",
                        column: x => x.ActiveCondominiumId,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_telegram_user_links_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_inbound_updates_ChatId_ReceivedAt",
                table: "telegram_inbound_updates",
                columns: new[] { "ChatId", "ReceivedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_inbound_updates_Status_NextAttemptAt",
                table: "telegram_inbound_updates",
                columns: new[] { "Status", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_inbound_updates_UpdateId",
                table: "telegram_inbound_updates",
                column: "UpdateId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_link_codes_CodeHash",
                table: "telegram_link_codes",
                column: "CodeHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_link_codes_UserId_ExpiresAt",
                table: "telegram_link_codes",
                columns: new[] { "UserId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_telegram_user_links_ActiveCondominiumId",
                table: "telegram_user_links",
                column: "ActiveCondominiumId");

            migrationBuilder.CreateIndex(
                name: "IX_telegram_user_links_TelegramChatId",
                table: "telegram_user_links",
                column: "TelegramChatId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_user_links_TelegramUserId",
                table: "telegram_user_links",
                column: "TelegramUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_telegram_user_links_UserId",
                table: "telegram_user_links",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "telegram_inbound_updates");

            migrationBuilder.DropTable(
                name: "telegram_link_codes");

            migrationBuilder.DropTable(
                name: "telegram_user_links");

            migrationBuilder.DropColumn(
                name: "channel",
                table: "condominium_assistant_conversations");

            migrationBuilder.DropColumn(
                name: "Channel",
                table: "assistant_execution_metrics");
        }
    }
}
