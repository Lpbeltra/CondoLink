using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations;

public partial class AddWhatsAppPhoneVerification : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "request_id",
            table: "whatsapp_outbound_messages",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AlterColumn<Guid>(
            name: "condominium_id",
            table: "whatsapp_outbound_messages",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.CreateTable(
            name: "whatsapp_phone_verifications",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                normalized_phone_number = table.Column<string>(
                    type: "character varying(14)", maxLength: 14,
                    nullable: false),
                code_hash = table.Column<byte[]>(
                    type: "bytea", nullable: false),
                code_salt = table.Column<byte[]>(
                    type: "bytea", nullable: false),
                attempt_count = table.Column<int>(
                    type: "integer", nullable: false),
                maximum_attempts = table.Column<int>(
                    type: "integer", nullable: false),
                created_at = table.Column<DateTime>(
                    type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(
                    type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(
                    type: "timestamp with time zone", nullable: false),
                confirmed_at = table.Column<DateTime>(
                    type: "timestamp with time zone", nullable: true),
                invalidated_at = table.Column<DateTime>(
                    type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_whatsapp_phone_verifications", x => x.id);
                table.ForeignKey(
                    name: "FK_whatsapp_phone_verifications_users_user_id",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_whatsapp_phone_verifications_phone_state_expiration",
            table: "whatsapp_phone_verifications",
            columns:
            [
                "normalized_phone_number",
                "confirmed_at",
                "invalidated_at",
                "expires_at"
            ]);

        migrationBuilder.CreateIndex(
            name: "IX_whatsapp_phone_verifications_user_id_created_at",
            table: "whatsapp_phone_verifications",
            columns: ["user_id", "created_at"]);

        migrationBuilder.CreateIndex(
            name: "ux_whatsapp_phone_verifications_active_user",
            table: "whatsapp_phone_verifications",
            column: "user_id",
            unique: true,
            filter:
                "\"confirmed_at\" IS NULL AND \"invalidated_at\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "whatsapp_phone_verifications");

        migrationBuilder.AlterColumn<Guid>(
            name: "request_id",
            table: "whatsapp_outbound_messages",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            name: "condominium_id",
            table: "whatsapp_outbound_messages",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);
    }
}
