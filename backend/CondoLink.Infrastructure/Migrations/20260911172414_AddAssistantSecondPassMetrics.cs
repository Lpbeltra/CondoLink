using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantSecondPassMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "FinalTopCount",
                table: "assistant_execution_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NewCandidateCount",
                table: "assistant_execution_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PrimaryTopCount",
                table: "assistant_execution_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReusedPrimaryRanking",
                table: "assistant_execution_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SecondPassConsidered",
                table: "assistant_execution_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SecondPassExecuted",
                table: "assistant_execution_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SecondPassSkippedNoNewCandidates",
                table: "assistant_execution_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalTopCount",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "NewCandidateCount",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "PrimaryTopCount",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "ReusedPrimaryRanking",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "SecondPassConsidered",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "SecondPassExecuted",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "SecondPassSkippedNoNewCandidates",
                table: "assistant_execution_metrics");
        }
    }
}
