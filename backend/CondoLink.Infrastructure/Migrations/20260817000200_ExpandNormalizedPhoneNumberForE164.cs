using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260817000200_ExpandNormalizedPhoneNumberForE164")]
public partial class ExpandNormalizedPhoneNumberForE164 : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "normalized_phone_number",
            table: "users",
            type: "character varying(16)",
            maxLength: 16,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(14)",
            oldMaxLength: 14,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "normalized_phone_number",
            table: "whatsapp_phone_verifications",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(14)",
            oldMaxLength: 14);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "normalized_phone_number",
            table: "users",
            type: "character varying(14)",
            maxLength: 14,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(16)",
            oldMaxLength: 16,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            name: "normalized_phone_number",
            table: "whatsapp_phone_verifications",
            type: "character varying(14)",
            maxLength: 14,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(16)",
            oldMaxLength: 16);
    }
}
