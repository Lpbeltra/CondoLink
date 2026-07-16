using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_requests_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requests_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requests_units_target_unit_id",
                        column: x => x.target_unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_requests_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "request_status_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_status = table.Column<int>(type: "integer", nullable: true),
                    new_status = table.Column<int>(type: "integer", nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_request_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_request_status_history_requests_request_id",
                        column: x => x.request_id,
                        principalTable: "requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_request_status_history_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_request_status_history_changed_by_user_id",
                table: "request_status_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_request_status_history_request_id_created_at",
                table: "request_status_history",
                columns: new[] { "request_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_requests_author_user_id",
                table: "requests",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_requests_category_id",
                table: "requests",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_requests_condominium_id",
                table: "requests",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "IX_requests_created_at",
                table: "requests",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_requests_status",
                table: "requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_requests_target_unit_id",
                table: "requests",
                column: "target_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "request_status_history");

            migrationBuilder.DropTable(
                name: "requests");
        }
    }
}
