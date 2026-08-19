using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalObservability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ai_operation_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Operation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    TotalTokens = table.Column<int>(type: "integer", nullable: true),
                    ErrorCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_operation_metrics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "operational_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Component = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operational_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "worker_heartbeats",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InstanceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    ExpectedIntervalSeconds = table.Column<int>(type: "integer", nullable: false),
                    LastHeartbeatAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastStartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastCompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSucceeded = table.Column<bool>(type: "boolean", nullable: true),
                    LastProcessedItems = table.Column<int>(type: "integer", nullable: true),
                    LastFailureCount = table.Column<int>(type: "integer", nullable: true),
                    LastResultCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_worker_heartbeats", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_operation_metrics_Operation_Model_Timestamp",
                table: "ai_operation_metrics",
                columns: new[] { "Operation", "Model", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_operation_metrics_Timestamp",
                table: "ai_operation_metrics",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_operational_events_Timestamp",
                table: "operational_events",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_worker_heartbeats_WorkerName_InstanceId",
                table: "worker_heartbeats",
                columns: new[] { "WorkerName", "InstanceId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_operation_metrics");

            migrationBuilder.DropTable(
                name: "operational_events");

            migrationBuilder.DropTable(
                name: "worker_heartbeats");
        }
    }
}
