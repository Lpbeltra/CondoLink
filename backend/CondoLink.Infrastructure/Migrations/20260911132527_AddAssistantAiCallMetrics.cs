using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantAiCallMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assistant_ai_call_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    TimedOut = table.Column<bool>(type: "boolean", nullable: false),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    CandidateCount = table.Column<int>(type: "integer", nullable: true),
                    PayloadBytes = table.Column<int>(type: "integer", nullable: true),
                    ErrorCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_ai_call_metrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assistant_ai_call_metrics_AssistantExecutionId_Ordinal",
                table: "assistant_ai_call_metrics",
                columns: new[] { "AssistantExecutionId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assistant_ai_call_metrics_Operation_Timestamp",
                table: "assistant_ai_call_metrics",
                columns: new[] { "Operation", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_assistant_ai_call_metrics_Timestamp",
                table: "assistant_ai_call_metrics",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_ai_call_metrics");
        }
    }
}
