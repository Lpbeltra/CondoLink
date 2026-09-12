using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeDocumentsAndDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "employee_document_id",
                table: "whatsapp_outbound_messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "employee_document_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    competence_month = table.Column<int>(type: "integer", nullable: false),
                    competence_year = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    pending_uploads_json = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_document_batches", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_document_batches_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_document_batches_users_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_document_batches_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    document_type = table.Column<int>(type: "integer", nullable: false),
                    competence_month = table.Column<int>(type: "integer", nullable: false),
                    competence_year = table.Column<int>(type: "integer", nullable: false),
                    file_key = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    original_file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    page_start = table.Column<int>(type: "integer", nullable: false),
                    page_end = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    identification_status = table.Column<int>(type: "integer", nullable: false),
                    identification_confidence = table.Column<int>(type: "integer", nullable: false),
                    identification_method = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_documents_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_documents_employee_document_batches_batch_id",
                        column: x => x.batch_id,
                        principalTable: "employee_document_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_documents_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_document_deliveries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    outbound_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    queued_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    queued_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_document_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_employee_document_deliveries_condominiums_condominium_id",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_document_deliveries_employee_document_batches_batc~",
                        column: x => x.batch_id,
                        principalTable: "employee_document_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_document_deliveries_employee_documents_employee_do~",
                        column: x => x.employee_document_id,
                        principalTable: "employee_documents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_employee_document_deliveries_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_document_deliveries_users_queued_by_user_id",
                        column: x => x.queued_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_document_deliveries_whatsapp_outbound_messages_out~",
                        column: x => x.outbound_message_id,
                        principalTable: "whatsapp_outbound_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_whatsapp_outbound_messages_employee_document_id",
                table: "whatsapp_outbound_messages",
                column: "employee_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_document_batches_condominium_id_created_at",
                table: "employee_document_batches",
                columns: new[] { "condominium_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_document_batches_condominium_id_type_competence",
                table: "employee_document_batches",
                columns: new[] { "condominium_id", "document_type", "competence_year", "competence_month" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_document_batches_confirmed_by_user_id",
                table: "employee_document_batches",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_document_batches_created_by_user_id",
                table: "employee_document_batches",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_document_deliveries_batch_id",
                table: "employee_document_deliveries",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_document_deliveries_condominium_id",
                table: "employee_document_deliveries",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_document_deliveries_employee_id",
                table: "employee_document_deliveries",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_document_deliveries_outbound_message_id",
                table: "employee_document_deliveries",
                column: "outbound_message_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_document_deliveries_queued_by_user_id",
                table: "employee_document_deliveries",
                column: "queued_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ux_employee_document_deliveries_document_id_channel",
                table: "employee_document_deliveries",
                columns: new[] { "employee_document_id", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_batch_id",
                table: "employee_documents",
                column: "batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_condominium_id_employee_id",
                table: "employee_documents",
                columns: new[] { "condominium_id", "employee_id" });

            migrationBuilder.CreateIndex(
                name: "ix_employee_documents_duplicate_lookup",
                table: "employee_documents",
                columns: new[] { "condominium_id", "employee_id", "document_type", "competence_year", "competence_month", "content_hash" });

            migrationBuilder.CreateIndex(
                name: "IX_employee_documents_employee_id",
                table: "employee_documents",
                column: "employee_id");

            migrationBuilder.AddForeignKey(
                name: "FK_whatsapp_outbound_messages_employee_documents_employee_docu~",
                table: "whatsapp_outbound_messages",
                column: "employee_document_id",
                principalTable: "employee_documents",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_whatsapp_outbound_messages_employee_documents_employee_docu~",
                table: "whatsapp_outbound_messages");

            migrationBuilder.DropTable(
                name: "employee_document_deliveries");

            migrationBuilder.DropTable(
                name: "employee_documents");

            migrationBuilder.DropTable(
                name: "employee_document_batches");

            migrationBuilder.DropIndex(
                name: "IX_whatsapp_outbound_messages_employee_document_id",
                table: "whatsapp_outbound_messages");

            migrationBuilder.DropColumn(
                name: "employee_document_id",
                table: "whatsapp_outbound_messages");
        }
    }
}
