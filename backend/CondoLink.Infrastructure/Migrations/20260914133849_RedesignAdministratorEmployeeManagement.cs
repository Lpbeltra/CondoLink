using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RedesignAdministratorEmployeeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cpf",
                table: "employees",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_cpf",
                table: "employees",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "management_company_modules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    management_company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_modules", x => x.id);
                    table.ForeignKey(
                        name: "FK_management_company_modules_management_companies_management_~",
                        column: x => x.management_company_id,
                        principalTable: "management_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_management_company_modules_company_module",
                table: "management_company_modules",
                columns: new[] { "management_company_id", "module" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "management_company_modules");

            migrationBuilder.DropColumn(
                name: "cpf",
                table: "employees");

            migrationBuilder.DropColumn(
                name: "normalized_cpf",
                table: "employees");
        }
    }
}
