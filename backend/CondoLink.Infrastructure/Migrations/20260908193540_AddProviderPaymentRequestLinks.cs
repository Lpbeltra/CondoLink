using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderPaymentRequestLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "request_id",
                table: "management_company_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "service_provider_id",
                table: "management_company_payment_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "third_party_pix_key_type",
                table: "management_company_payment_requests",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_request_id",
                table: "management_company_requests",
                column: "request_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_payment_requests_service_provider_id",
                table: "management_company_payment_requests",
                column: "service_provider_id");

            migrationBuilder.AddForeignKey(
                name: "FK_management_company_payment_requests_service_providers_servi~",
                table: "management_company_payment_requests",
                column: "service_provider_id",
                principalTable: "service_providers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_management_company_requests_requests_request_id",
                table: "management_company_requests",
                column: "request_id",
                principalTable: "requests",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_management_company_payment_requests_service_providers_servi~",
                table: "management_company_payment_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_management_company_requests_requests_request_id",
                table: "management_company_requests");

            migrationBuilder.DropIndex(
                name: "IX_management_company_requests_request_id",
                table: "management_company_requests");

            migrationBuilder.DropIndex(
                name: "IX_management_company_payment_requests_service_provider_id",
                table: "management_company_payment_requests");

            migrationBuilder.DropColumn(
                name: "request_id",
                table: "management_company_requests");

            migrationBuilder.DropColumn(
                name: "service_provider_id",
                table: "management_company_payment_requests");

            migrationBuilder.DropColumn(
                name: "third_party_pix_key_type",
                table: "management_company_payment_requests");
        }
    }
}
