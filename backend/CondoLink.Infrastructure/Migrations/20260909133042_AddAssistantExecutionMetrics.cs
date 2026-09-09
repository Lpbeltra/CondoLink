using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAssistantExecutionMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "assistant_execution_metrics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AssistantExecutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CondominiumId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    TotalDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    RetrievalDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ExpansionDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    EmbeddingDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    DatabaseMaterializationDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    EmbeddingDeserializationDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    VectorScoringDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    LexicalScoringDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    RerankDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    RerankFallbackDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ContextPreparationDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    ChatDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    TimeToFirstTokenMs = table.Column<long>(type: "bigint", nullable: true),
                    GenerationDurationMs = table.Column<long>(type: "bigint", nullable: true),
                    EligibleDocuments = table.Column<int>(type: "integer", nullable: true),
                    EligibleChunks = table.Column<int>(type: "integer", nullable: true),
                    LoadedChunks = table.Column<int>(type: "integer", nullable: true),
                    DeserializedEmbeddings = table.Column<int>(type: "integer", nullable: true),
                    ExpandedQueries = table.Column<int>(type: "integer", nullable: true),
                    CandidatesBeforeRerank = table.Column<int>(type: "integer", nullable: true),
                    CandidatesAfterRerank = table.Column<int>(type: "integer", nullable: true),
                    FinalChunks = table.Column<int>(type: "integer", nullable: true),
                    ContextCharacters = table.Column<int>(type: "integer", nullable: true),
                    ContextTokensApprox = table.Column<int>(type: "integer", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ChatModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    InputTokens = table.Column<int>(type: "integer", nullable: true),
                    OutputTokens = table.Column<int>(type: "integer", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: true),
                    RerankFallbackUsed = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCategory = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_execution_metrics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assistant_execution_metrics_AssistantExecutionId",
                table: "assistant_execution_metrics",
                column: "AssistantExecutionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_assistant_execution_metrics_CondominiumId_StartedAt",
                table: "assistant_execution_metrics",
                columns: new[] { "CondominiumId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_assistant_execution_metrics_StartedAt",
                table: "assistant_execution_metrics",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_assistant_execution_metrics_Success_StartedAt",
                table: "assistant_execution_metrics",
                columns: new[] { "Success", "StartedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assistant_execution_metrics");
        }
    }
}
