using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationManagementCompanyRequestSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "idempotency_key",
                table: "notifications",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "management_company_request_id",
                table: "notifications",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_management_company_request_id",
                table: "notifications",
                column: "management_company_request_id");

            migrationBuilder.CreateIndex(
                name: "ux_notifications_idempotency_key",
                table: "notifications",
                column: "idempotency_key",
                unique: true,
                filter: "\"idempotency_key\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_notifications_management_company_requests_management_compan~",
                table: "notifications",
                column: "management_company_request_id",
                principalTable: "management_company_requests",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_notifications_management_company_requests_management_compan~",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_management_company_request_id",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ux_notifications_idempotency_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "idempotency_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "management_company_request_id",
                table: "notifications");
        }
    }
}
