using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeEmployeeDocumentBatchesManagementCompanyScoped : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employee_document_batches_condominiums_condominium_id",
                table: "employee_document_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_documents_condominiums_condominium_id",
                table: "employee_documents");

            migrationBuilder.AlterColumn<Guid>(
                name: "condominium_id",
                table: "employee_documents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "condominium_id",
                table: "employee_document_batches",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "management_company_id",
                table: "employee_document_batches",
                type: "uuid",
                nullable: true);

            // Preserve historical batches without inventing a relationship: a
            // management company is backfilled only from the batch's existing
            // condominium when that condominium is currently linked.
            migrationBuilder.Sql("""
                UPDATE employee_document_batches b
                SET management_company_id = c.management_company_id
                FROM condominiums c
                WHERE b.condominium_id = c.id
                  AND c.management_company_id IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "ix_employee_document_batches_management_company_id_created_at",
                table: "employee_document_batches",
                columns: new[] { "management_company_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_employee_document_batches_condominiums_condominium_id",
                table: "employee_document_batches",
                column: "condominium_id",
                principalTable: "condominiums",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_document_batches_management_companies_management_c~",
                table: "employee_document_batches",
                column: "management_company_id",
                principalTable: "management_companies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_documents_condominiums_condominium_id",
                table: "employee_documents",
                column: "condominium_id",
                principalTable: "condominiums",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_employee_document_batches_condominiums_condominium_id",
                table: "employee_document_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_document_batches_management_companies_management_c~",
                table: "employee_document_batches");

            migrationBuilder.DropForeignKey(
                name: "FK_employee_documents_condominiums_condominium_id",
                table: "employee_documents");

            migrationBuilder.DropIndex(
                name: "ix_employee_document_batches_management_company_id_created_at",
                table: "employee_document_batches");

            migrationBuilder.DropColumn(
                name: "management_company_id",
                table: "employee_document_batches");

            migrationBuilder.AlterColumn<Guid>(
                name: "condominium_id",
                table: "employee_documents",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "condominium_id",
                table: "employee_document_batches",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_document_batches_condominiums_condominium_id",
                table: "employee_document_batches",
                column: "condominium_id",
                principalTable: "condominiums",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_employee_documents_condominiums_condominium_id",
                table: "employee_documents",
                column: "condominium_id",
                principalTable: "condominiums",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
