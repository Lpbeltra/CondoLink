using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWhatsAppLoginChallenges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_whatsapp_phone_verifications_phone_state_expiration",
                table: "whatsapp_phone_verifications");

            migrationBuilder.DropIndex(
                name: "ux_whatsapp_phone_verifications_active_user",
                table: "whatsapp_phone_verifications");

            migrationBuilder.AddColumn<DateTime>(
                name: "consumed_at",
                table: "whatsapp_phone_verifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "purpose",
                table: "whatsapp_phone_verifications",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "version",
                table: "whatsapp_phone_verifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(
                """
                UPDATE whatsapp_phone_verifications
                SET consumed_at = confirmed_at
                WHERE confirmed_at IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_phone_verifications_phone_state_expiration",
                table: "whatsapp_phone_verifications",
                columns: new[] { "normalized_phone_number", "purpose", "confirmed_at", "consumed_at", "invalidated_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_whatsapp_phone_verifications_active_user_purpose",
                table: "whatsapp_phone_verifications",
                columns: new[] { "user_id", "purpose" },
                unique: true,
                filter: "\"consumed_at\" IS NULL AND \"invalidated_at\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DELETE FROM whatsapp_phone_verifications
                WHERE purpose = 2;
                """);

            migrationBuilder.DropIndex(
                name: "ix_whatsapp_phone_verifications_phone_state_expiration",
                table: "whatsapp_phone_verifications");

            migrationBuilder.DropIndex(
                name: "ux_whatsapp_phone_verifications_active_user_purpose",
                table: "whatsapp_phone_verifications");

            migrationBuilder.DropColumn(
                name: "consumed_at",
                table: "whatsapp_phone_verifications");

            migrationBuilder.DropColumn(
                name: "purpose",
                table: "whatsapp_phone_verifications");

            migrationBuilder.DropColumn(
                name: "version",
                table: "whatsapp_phone_verifications");

            migrationBuilder.CreateIndex(
                name: "ix_whatsapp_phone_verifications_phone_state_expiration",
                table: "whatsapp_phone_verifications",
                columns: new[] { "normalized_phone_number", "confirmed_at", "invalidated_at", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_whatsapp_phone_verifications_active_user",
                table: "whatsapp_phone_verifications",
                column: "user_id",
                unique: true,
                filter: "\"confirmed_at\" IS NULL AND \"invalidated_at\" IS NULL");
        }
    }
}
