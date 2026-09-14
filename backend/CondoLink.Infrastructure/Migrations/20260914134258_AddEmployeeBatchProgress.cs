using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeBatchProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "processed_items",
                table: "employee_document_batches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "processing_stage",
                table: "employee_document_batches",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "total_items",
                table: "employee_document_batches",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "processed_items",
                table: "employee_document_batches");

            migrationBuilder.DropColumn(
                name: "processing_stage",
                table: "employee_document_batches");

            migrationBuilder.DropColumn(
                name: "total_items",
                table: "employee_document_batches");
        }
    }
}
