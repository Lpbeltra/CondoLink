using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ScopeManagementCompanyEmployeePermissionsByCondominium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_mc_employee_module_permissions_employee_module",
                table: "management_company_employee_module_permissions");

            migrationBuilder.AddColumn<Guid>(
                name: "condominium_id",
                table: "management_company_employee_module_permissions",
                type: "uuid",
                nullable: true);

            // A legacy global allow cannot safely be assigned to any one
            // condominium. Keep the audit row, but revoke existing global
            // grants; the new access path also ignores rows without a scope.
            migrationBuilder.Sql("""
                UPDATE management_company_employee_module_permissions
                SET is_allowed = FALSE, revoked_at = COALESCE(revoked_at, now())
                WHERE condominium_id IS NULL AND is_allowed = TRUE;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_management_company_employee_module_permissions_condominium_~",
                table: "management_company_employee_module_permissions",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "ux_mc_employee_module_permissions_employee_condominium_module",
                table: "management_company_employee_module_permissions",
                columns: new[] { "management_company_employee_id", "condominium_id", "module" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_management_company_employee_module_permissions_condominiums~",
                table: "management_company_employee_module_permissions",
                column: "condominium_id",
                principalTable: "condominiums",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The old unique (employee,module) constraint cannot represent
            // multiple condominium grants. Refuse lossy automatic rollback.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (
                        SELECT 1 FROM management_company_employee_module_permissions
                        WHERE condominium_id IS NOT NULL
                    ) THEN
                        RAISE EXCEPTION 'Scoped employee permissions exist; export/revoke them before rollback.';
                    END IF;
                END $$;
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_management_company_employee_module_permissions_condominiums~",
                table: "management_company_employee_module_permissions");

            migrationBuilder.DropIndex(
                name: "IX_management_company_employee_module_permissions_condominium_~",
                table: "management_company_employee_module_permissions");

            migrationBuilder.DropIndex(
                name: "ux_mc_employee_module_permissions_employee_condominium_module",
                table: "management_company_employee_module_permissions");

            migrationBuilder.DropColumn(
                name: "condominium_id",
                table: "management_company_employee_module_permissions");

            migrationBuilder.CreateIndex(
                name: "ux_mc_employee_module_permissions_employee_module",
                table: "management_company_employee_module_permissions",
                columns: new[] { "management_company_employee_id", "module" },
                unique: true);
        }
    }
}
