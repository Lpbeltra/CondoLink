using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManagementCompanyFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "pix_key",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pix_key_type",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "access_type",
                table: "management_company_employees",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Person");

            migrationBuilder.CreateTable(
                name: "condominium_management_company_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false),
                    management_company_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    linked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    unlinked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_condominium_management_company_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_condominium_management_company_links_condominiums_condomini~",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_condominium_management_company_links_management_companies_m~",
                        column: x => x.management_company_id,
                        principalTable: "management_companies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "management_company_request_category_responsibles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    access_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_management_company_request_category_responsibles", x => x.id);
                    table.ForeignKey(
                        name: "FK_management_company_request_category_responsibles_management~",
                        column: x => x.access_id,
                        principalTable: "management_company_employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_management_company_request_category_responsibles_managemen~1",
                        column: x => x.category_id,
                        principalTable: "management_company_request_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_condominium_management_company_links_management_company_id_~",
                table: "condominium_management_company_links",
                columns: new[] { "management_company_id", "condominium_id" });

            migrationBuilder.CreateIndex(
                name: "ux_condominium_management_company_links_active_condominium",
                table: "condominium_management_company_links",
                column: "condominium_id",
                unique: true,
                filter: "\"is_active\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "IX_management_company_request_category_responsibles_access_id",
                table: "management_company_request_category_responsibles",
                column: "access_id");

            migrationBuilder.CreateIndex(
                name: "ux_mc_category_responsibles_category_access",
                table: "management_company_request_category_responsibles",
                columns: new[] { "category_id", "access_id" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO condominium_management_company_links
                    (id, condominium_id, management_company_id, is_active, linked_at, unlinked_at)
                SELECT (substr(md5(c.id::text || ':management-company-link'),1,8) || '-' ||
                        substr(md5(c.id::text || ':management-company-link'),9,4) || '-' ||
                        substr(md5(c.id::text || ':management-company-link'),13,4) || '-' ||
                        substr(md5(c.id::text || ':management-company-link'),17,4) || '-' ||
                        substr(md5(c.id::text || ':management-company-link'),21,12))::uuid,
                       c.id, c.management_company_id, TRUE, c.updated_at, NULL
                FROM condominiums c
                WHERE c.management_company_id IS NOT NULL;

                INSERT INTO management_company_request_categories
                    (id, management_company_id, name, normalized_name, description, form_type, is_active, created_at, updated_at)
                SELECT (substr(md5(mc.id::text || ':' || v.normalized_name),1,8) || '-' ||
                        substr(md5(mc.id::text || ':' || v.normalized_name),9,4) || '-' ||
                        substr(md5(mc.id::text || ':' || v.normalized_name),13,4) || '-' ||
                        substr(md5(mc.id::text || ':' || v.normalized_name),17,4) || '-' ||
                        substr(md5(mc.id::text || ':' || v.normalized_name),21,12))::uuid,
                       mc.id, v.name, v.normalized_name, NULL, v.form_type, TRUE, NOW(), NOW()
                FROM management_companies mc
                CROSS JOIN (VALUES
                    ('Multa', 'MULTA', 'UnitFine'),
                    ('Solicitação de pagamento', 'SOLICITAÇÃO DE PAGAMENTO', 'SupplierPayment'),
                    ('Dúvidas gerais', 'DÚVIDAS GERAIS', 'Generic')) v(name, normalized_name, form_type)
                ON CONFLICT (management_company_id, normalized_name) DO NOTHING;

                CREATE OR REPLACE FUNCTION enforce_single_active_submanager_role()
                RETURNS trigger LANGUAGE plpgsql AS $fn$
                DECLARE v_user uuid; v_condominium uuid;
                BEGIN
                  IF NEW.role <> 3 OR NOT NEW.is_active OR NEW.revoked_at IS NOT NULL THEN RETURN NEW; END IF;
                  SELECT user_id, condominium_id INTO v_user, v_condominium
                    FROM condominium_memberships WHERE id = NEW.condominium_membership_id;
                  PERFORM pg_advisory_xact_lock(hashtextextended(v_user::text, 9182));
                  PERFORM pg_advisory_xact_lock(hashtextextended(v_condominium::text, 9183));
                  IF EXISTS (
                    SELECT 1 FROM condominium_membership_roles r
                    JOIN condominium_memberships m ON m.id = r.condominium_membership_id
                    WHERE r.role = 3 AND r.is_active AND r.revoked_at IS NULL
                      AND m.is_active AND m.ended_at IS NULL AND r.id <> NEW.id
                      AND (m.user_id = v_user OR m.condominium_id = v_condominium))
                  THEN RAISE EXCEPTION 'Only one active submanager link is allowed per user and condominium'
                    USING ERRCODE = '23505'; END IF;
                  RETURN NEW;
                END $fn$;
                CREATE TRIGGER trg_single_active_submanager_role
                  BEFORE INSERT OR UPDATE ON condominium_membership_roles
                  FOR EACH ROW EXECUTE FUNCTION enforce_single_active_submanager_role();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_single_active_submanager_role ON condominium_membership_roles; DROP FUNCTION IF EXISTS enforce_single_active_submanager_role();");
            migrationBuilder.DropTable(
                name: "condominium_management_company_links");

            migrationBuilder.DropTable(
                name: "management_company_request_category_responsibles");

            migrationBuilder.DropColumn(
                name: "pix_key",
                table: "users");

            migrationBuilder.DropColumn(
                name: "pix_key_type",
                table: "users");

            migrationBuilder.DropColumn(
                name: "access_type",
                table: "management_company_employees");
        }
    }
}
