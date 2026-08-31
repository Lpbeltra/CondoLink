using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagementCompanyPaymentThirdPartyAndAttachmentPurpose : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "purpose",
                table: "management_company_request_attachments",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Request");

            migrationBuilder.AddColumn<DateOnly>(
                name: "due_date",
                table: "management_company_payment_requests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "third_party_account",
                table: "management_company_payment_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "third_party_agency",
                table: "management_company_payment_requests",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "third_party_bank",
                table: "management_company_payment_requests",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "third_party_form",
                table: "management_company_payment_requests",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "third_party_identification",
                table: "management_company_payment_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "third_party_pix_key",
                table: "management_company_payment_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "purpose",
                table: "management_company_request_attachments");

            migrationBuilder.DropColumn(
                name: "due_date",
                table: "management_company_payment_requests");

            migrationBuilder.DropColumn(
                name: "third_party_account",
                table: "management_company_payment_requests");

            migrationBuilder.DropColumn(
                name: "third_party_agency",
                table: "management_company_payment_requests");

            migrationBuilder.DropColumn(
                name: "third_party_bank",
                table: "management_company_payment_requests");

            migrationBuilder.DropColumn(
                name: "third_party_form",
                table: "management_company_payment_requests");

            migrationBuilder.DropColumn(
                name: "third_party_identification",
                table: "management_company_payment_requests");

            migrationBuilder.DropColumn(
                name: "third_party_pix_key",
                table: "management_company_payment_requests");
        }
    }
}
