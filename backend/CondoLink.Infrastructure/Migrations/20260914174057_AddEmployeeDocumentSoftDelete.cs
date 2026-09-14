using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDocumentSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "employee_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_user_id",
                table: "employee_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_documents_deleted_by_user_id",
                table: "employee_documents",
                column: "deleted_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "FK_employee_documents_users_deleted_by_user_id",
                table: "employee_documents",
                column: "deleted_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employee_documents_users_deleted_by_user_id",
                table: "employee_documents");

            migrationBuilder.DropIndex(
                name: "IX_employee_documents_deleted_by_user_id",
                table: "employee_documents");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "employee_documents");

            migrationBuilder.DropColumn(
                name: "deleted_by_user_id",
                table: "employee_documents");
        }
    }
}
