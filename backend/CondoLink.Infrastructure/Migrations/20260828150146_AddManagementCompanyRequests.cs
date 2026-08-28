using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagementCompanyRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "management_company_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    friendly_identifier = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    management_company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    concurrency_stamp = table.Column<Guid>(type: "uuid", nullable: false),
                    acknowledged_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancelled_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_management_company_requests_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_requests_management_companies_management~",
                        column: x => x.management_company_id,
                        principalTable: "management_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_requests_management_company_request_cate~",
                        column: x => x.category_id,
                        principalTable: "management_company_request_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_requests_users_acknowledged_by_user_id",
                        column: x => x.acknowledged_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_requests_users_cancelled_by_user_id",
                        column: x => x.cancelled_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_requests_users_completed_by_user_id",
                        column: x => x.completed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_requests_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "management_company_fine_requests",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    occurrence_date = table.Column<DateOnly>(type: "date", nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    value_not_defined = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_fine_requests", x => x.request_id);
                    table.CheckConstraint("ck_mc_fine_value", "(value_not_defined AND value IS NULL) OR (NOT value_not_defined AND value IS NOT NULL AND value >= 0)");
                    table.ForeignKey(
                        name: "FK_management_company_fine_requests_management_company_request~",
                        column: x => x.request_id,
                        principalTable: "management_company_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_fine_requests_units_unit_id",
                        column: x => x.unit_id,
                        principalTable: "units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "management_company_general_question_requests",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    theme = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_general_question_requests", x => x.request_id);
                    table.ForeignKey(
                        name: "FK_management_company_general_question_requests_management_com~",
                        column: x => x.request_id,
                        principalTable: "management_company_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "management_company_payment_requests",
                columns: table => new
                {
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nature = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    value = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    event_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_reimbursement = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    beneficiary_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    beneficiary_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pix_key_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pix_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_payment_requests", x => x.request_id);
                    table.CheckConstraint("ck_mc_payment_reimbursement", "(is_reimbursement AND beneficiary_user_id IS NOT NULL AND beneficiary_name IS NOT NULL AND pix_key_type IS NOT NULL AND pix_key IS NOT NULL) OR (NOT is_reimbursement AND beneficiary_user_id IS NULL AND beneficiary_name IS NULL AND pix_key_type IS NULL AND pix_key IS NULL)");
                    table.ForeignKey(
                        name: "FK_management_company_payment_requests_management_company_requ~",
                        column: x => x.request_id,
                        principalTable: "management_company_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_payment_requests_users_beneficiary_user_~",
                        column: x => x.beneficiary_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "management_company_request_history",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    previous_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    new_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_request_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_management_company_request_history_management_company_reque~",
                        column: x => x.request_id,
                        principalTable: "management_company_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_request_history_users_changed_by_user_id",
                        column: x => x.changed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "management_company_request_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_request_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_management_company_request_messages_management_company_requ~",
                        column: x => x.request_id,
                        principalTable: "management_company_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_request_messages_users_author_user_id",
                        column: x => x.author_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "management_company_request_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_request_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_management_company_request_attachments_management_company_r~",
                        column: x => x.message_id,
                        principalTable: "management_company_request_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_request_attachments_management_company_~1",
                        column: x => x.request_id,
                        principalTable: "management_company_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_request_attachments_users_uploaded_by_us~",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_management_company_fine_requests_unit_id",
                table: "management_company_fine_requests",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_payment_requests_beneficiary_user_id",
                table: "management_company_payment_requests",
                column: "beneficiary_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_request_attachments_message_id",
                table: "management_company_request_attachments",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_request_attachments_request_id_created_at",
                table: "management_company_request_attachments",
                columns: new[] { "request_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_management_company_request_attachments_uploaded_by_user_id",
                table: "management_company_request_attachments",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_request_history_changed_by_user_id",
                table: "management_company_request_history",
                column: "changed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_request_history_request_id_created_at",
                table: "management_company_request_history",
                columns: new[] { "request_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_mc_request_history_first_acknowledged",
                table: "management_company_request_history",
                column: "request_id",
                unique: true,
                filter: "\"event_type\" = 'Acknowledged'");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_request_messages_author_user_id",
                table: "management_company_request_messages",
                column: "author_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_request_messages_request_id_created_at",
                table: "management_company_request_messages",
                columns: new[] { "request_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_acknowledged_by_user_id",
                table: "management_company_requests",
                column: "acknowledged_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_cancelled_by_user_id",
                table: "management_company_requests",
                column: "cancelled_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_category_id",
                table: "management_company_requests",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_completed_by_user_id",
                table: "management_company_requests",
                column: "completed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_condominium_id_status_updated_at",
                table: "management_company_requests",
                columns: new[] { "condominium_id", "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_created_at",
                table: "management_company_requests",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_created_by_user_id",
                table: "management_company_requests",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_requests_management_company_id_category_~",
                table: "management_company_requests",
                columns: new[] { "management_company_id", "category_id", "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ux_management_company_requests_friendly_identifier",
                table: "management_company_requests",
                column: "friendly_identifier",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "management_company_fine_requests");

            migrationBuilder.DropTable(
                name: "management_company_general_question_requests");

            migrationBuilder.DropTable(
                name: "management_company_payment_requests");

            migrationBuilder.DropTable(
                name: "management_company_request_attachments");

            migrationBuilder.DropTable(
                name: "management_company_request_history");

            migrationBuilder.DropTable(
                name: "management_company_request_messages");

            migrationBuilder.DropTable(
                name: "management_company_requests");
        }
    }
}
