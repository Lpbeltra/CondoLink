using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace CondoLink.Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260817000300_AddCondominiumAssistant")]
public partial class AddCondominiumAssistant : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("condominium_documents", table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
            name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), document_type = table.Column<int>(type: "integer", nullable: false),
            original_file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false), storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            mime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false), version = table.Column<int>(type: "integer", nullable: false),
            document_date = table.Column<DateOnly>(type: "date", nullable: true), is_active = table.Column<bool>(type: "boolean", nullable: false),
            created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false), updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false), processing_status = table.Column<int>(type: "integer", nullable: false),
            processing_error = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
        }, constraints: table => { table.PrimaryKey("PK_condominium_documents", x => x.id);
            table.ForeignKey("FK_documents_condominiums", x => x.condominium_id, "condominiums", "id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_documents_users", x => x.uploaded_by_user_id, "users", "id", onDelete: ReferentialAction.Restrict); });
        m.CreateTable("condominium_assistant_conversations", table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
            created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false), request_id = table.Column<Guid>(type: "uuid", nullable: true),
            title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false), created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => { table.PrimaryKey("PK_condominium_assistant_conversations", x => x.id);
            table.ForeignKey("FK_assistant_conversations_condominiums", x => x.condominium_id, "condominiums", "id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_assistant_conversations_users", x => x.created_by_user_id, "users", "id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_assistant_conversations_requests", x => x.request_id, "requests", "id", onDelete: ReferentialAction.SetNull); });
        m.CreateTable("condominium_document_chunks", table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), condominium_document_id = table.Column<Guid>(type: "uuid", nullable: false), condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
            chunk_index = table.Column<int>(type: "integer", nullable: false), content = table.Column<string>(type: "text", nullable: false), embedding = table.Column<string>(type: "text", nullable: false),
            page_number = table.Column<int>(type: "integer", nullable: true), section_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true), created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => { table.PrimaryKey("PK_condominium_document_chunks", x => x.id);
            table.ForeignKey("FK_document_chunks_documents", x => x.condominium_document_id, "condominium_documents", "id", onDelete: ReferentialAction.Cascade);
            table.ForeignKey("FK_document_chunks_condominiums", x => x.condominium_id, "condominiums", "id", onDelete: ReferentialAction.Cascade); });
        m.CreateTable("condominium_assistant_messages", table => new
        {
            id = table.Column<Guid>(type: "uuid", nullable: false), conversation_id = table.Column<Guid>(type: "uuid", nullable: false), role = table.Column<int>(type: "integer", nullable: false),
            content = table.Column<string>(type: "text", nullable: false), sources_json = table.Column<string>(type: "text", nullable: true), created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: table => { table.PrimaryKey("PK_condominium_assistant_messages", x => x.id);
            table.ForeignKey("FK_assistant_messages_conversations", x => x.conversation_id, "condominium_assistant_conversations", "id", onDelete: ReferentialAction.Cascade); });
        m.CreateIndex("IX_documents_scope", "condominium_documents", new[] { "condominium_id", "is_active", "processing_status" });
        m.CreateIndex("IX_documents_uploaded_by", "condominium_documents", "uploaded_by_user_id");
        m.CreateIndex("IX_chunks_scope", "condominium_document_chunks", new[] { "condominium_id", "condominium_document_id" });
        m.CreateIndex("UX_chunks_document_index", "condominium_document_chunks", new[] { "condominium_document_id", "chunk_index" }, unique: true);
        m.CreateIndex("IX_conversations_owner", "condominium_assistant_conversations", new[] { "condominium_id", "created_by_user_id", "updated_at" });
        m.CreateIndex("IX_conversations_request", "condominium_assistant_conversations", "request_id");
        m.CreateIndex("IX_messages_conversation", "condominium_assistant_messages", new[] { "conversation_id", "created_at" });
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("condominium_assistant_messages"); m.DropTable("condominium_document_chunks");
        m.DropTable("condominium_assistant_conversations"); m.DropTable("condominium_documents");
    }
}
