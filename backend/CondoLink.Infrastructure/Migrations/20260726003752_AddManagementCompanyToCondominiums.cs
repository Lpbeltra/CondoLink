using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagementCompanyToCondominiums : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "management_company_id",
                table: "condominiums",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_condominiums_management_company_id",
                table: "condominiums",
                column: "management_company_id");

            migrationBuilder.AddForeignKey(
                name: "FK_condominiums_management_companies_management_company_id",
                table: "condominiums",
                column: "management_company_id",
                principalTable: "management_companies",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_condominiums_management_companies_management_company_id",
                table: "condominiums");

            migrationBuilder.DropIndex(
                name: "ix_condominiums_management_company_id",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "management_company_id",
                table: "condominiums");
        }
    }
}
