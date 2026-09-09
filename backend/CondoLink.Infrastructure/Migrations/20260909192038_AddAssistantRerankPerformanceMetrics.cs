using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantRerankPerformanceMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RerankAttempted",
                table: "assistant_execution_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RerankCandidatesSent",
                table: "assistant_execution_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RerankFastPathUsed",
                table: "assistant_execution_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RerankInputTokensApprox",
                table: "assistant_execution_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RerankModel",
                table: "assistant_execution_metrics",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RerankPayloadBytes",
                table: "assistant_execution_metrics",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RerankSucceeded",
                table: "assistant_execution_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RerankTimedOut",
                table: "assistant_execution_metrics",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RerankAttempted",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "RerankCandidatesSent",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "RerankFastPathUsed",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "RerankInputTokensApprox",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "RerankModel",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "RerankPayloadBytes",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "RerankSucceeded",
                table: "assistant_execution_metrics");

            migrationBuilder.DropColumn(
                name: "RerankTimedOut",
                table: "assistant_execution_metrics");
        }
    }
}
