using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    job_title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    normalized_phone_number = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    registration_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    normalized_registration_number = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    admission_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                    table.ForeignKey(
                        name: "FK_employees_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "management_company_employee_module_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    management_company_employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<int>(type: "integer", nullable: false),
                    is_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_employee_module_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_management_company_employee_module_permissions_management_c~",
                        column: x => x.management_company_employee_id,
                        principalTable: "management_company_employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employees_condominium_id",
                table: "employees",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "ux_employees_condominium_id_registration_number",
                table: "employees",
                columns: new[] { "condominium_id", "normalized_registration_number" },
                unique: true,
                filter: "normalized_registration_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_mc_employee_module_permissions_employee_module",
                table: "management_company_employee_module_permissions",
                columns: new[] { "management_company_employee_id", "module" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "management_company_employee_module_permissions");
        }
    }
}
