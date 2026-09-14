using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeBatchSelection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "employee_document_batch_employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeDocumentBatchId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_document_batch_employees", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_document_batch_employees_employee_document_batches~",
                        column: x => x.batch_id,
                        principalTable: "employee_document_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_document_batch_employees_employee_document_batche~1",
                        column: x => x.EmployeeDocumentBatchId,
                        principalTable: "employee_document_batches",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_employee_document_batch_employees_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_employee_document_batch_employees_employee_id",
                table: "employee_document_batch_employees",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_document_batch_employees_EmployeeDocumentBatchId",
                table: "employee_document_batch_employees",
                column: "EmployeeDocumentBatchId");

            migrationBuilder.CreateIndex(
                name: "ux_employee_document_batch_employees",
                table: "employee_document_batch_employees",
                columns: new[] { "batch_id", "employee_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_document_batch_employees");
        }
    }
}
