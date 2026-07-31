using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestAiAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "request_ai_analyses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    generated_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    generated_description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    suggested_category_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    confidence = table.Column<double>(type: "double precision", nullable: true),
                    missing_information_json = table.Column<string>(type: "text", nullable: false),
                    ai_model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_ai_analyses", x => x.id);
                    table.ForeignKey(
                        name: "FK_request_ai_analyses_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_request_ai_analyses_request_id",
                table: "request_ai_analyses",
                column: "request_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_ai_analyses");
        }
    }
}
