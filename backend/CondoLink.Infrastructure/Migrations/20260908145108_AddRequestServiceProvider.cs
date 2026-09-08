using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestServiceProvider : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "service_provider_id",
                table: "requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "request_service_provider_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    previous_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    previous_specialty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    provider_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    provider_specialty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_service_provider_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_request_service_provider_history_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_request_service_provider_history_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_requests_service_provider_id",
                table: "requests",
                column: "service_provider_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_service_provider_history_changed_by_user_id",
                table: "request_service_provider_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_service_provider_history_request_id_created_at",
                table: "request_service_provider_history",
                columns: new[] { "request_id", "created_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_requests_service_providers_service_provider_id",
                table: "requests",
                column: "service_provider_id",
                principalTable: "service_providers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_requests_service_providers_service_provider_id",
                table: "requests");

            migrationBuilder.DropTable(
                name: "request_service_provider_history");

            migrationBuilder.DropIndex(
                name: "IX_requests_service_provider_id",
                table: "requests");

            migrationBuilder.DropColumn(
                name: "service_provider_id",
                table: "requests");
        }
    }
}
