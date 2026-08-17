using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using CondoLink.Infrastructure.Persistence;

#nullable disable
namespace CondoLink.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260817000100_AddRequestClosureConfirmations")]
public partial class AddRequestClosureConfirmations : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("request_closure_confirmations", table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), request_id = table.Column<Guid>(type: "uuid", nullable: false),
            request_status_history_id = table.Column<Guid>(type: "uuid", nullable: false), conclusion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            status = table.Column<int>(type: "integer", nullable: false), decided_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            response_message_id = table.Column<Guid>(type: "uuid", nullable: true), finalized_automatically = table.Column<bool>(type: "boolean", nullable: false),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table =>
        {
            table.PrimaryKey("PK_request_closure_confirmations", x => x.id);
            table.ForeignKey("FK_closure_request", x => x.request_id, "requests", "id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_closure_history", x => x.request_status_history_id, "request_status_history", "id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_closure_message", x => x.response_message_id, "request_messages", "id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_request_closure_confirmations_request_id", "request_closure_confirmations", "request_id", unique: true, filter: "status = 1");
        m.CreateIndex("IX_request_closure_confirmations_request_status_history_id", "request_closure_confirmations", "request_status_history_id");
        m.CreateIndex("IX_request_closure_confirmations_response_message_id", "request_closure_confirmations", "response_message_id");
        m.CreateIndex("IX_request_closure_confirmations_status_expires_at", "request_closure_confirmations", new[] { "status", "expires_at" });
    }
    protected override void Down(MigrationBuilder m) => m.DropTable("request_closure_confirmations");
}
