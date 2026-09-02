using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSubManagerModulePermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sub_manager_module_permissions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                    module = table.Column<int>(type: "integer", nullable: false),
                    is_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sub_manager_module_permissions", x => x.id);
                    table.ForeignKey(
                        name: "FK_sub_manager_module_permissions_condominium_memberships_cond~",
                        column: x => x.condominium_membership_id,
                        principalTable: "condominium_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_sub_manager_module_permissions_users_granted_by_user_id",
                        column: x => x.granted_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sub_manager_module_permissions_condominium_membership_id_mo~",
                table: "sub_manager_module_permissions",
                columns: new[] { "condominium_membership_id", "module" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sub_manager_module_permissions_granted_by_user_id",
                table: "sub_manager_module_permissions",
                column: "granted_by_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sub_manager_module_permissions");
        }
    }
}
