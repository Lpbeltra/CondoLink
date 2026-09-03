using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations;

public partial class RestoreGlobalSubManagerUniqueness : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_single_active_submanager_role ON condominium_membership_roles; DROP FUNCTION IF EXISTS enforce_single_active_submanager_role();");
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION enforce_single_active_submanager_role()
            RETURNS trigger LANGUAGE plpgsql AS $fn$
            DECLARE v_user uuid;
            BEGIN
              IF NEW.role <> 3 OR NOT NEW.is_active OR NEW.revoked_at IS NOT NULL THEN RETURN NEW; END IF;
              SELECT user_id INTO v_user
                FROM condominium_memberships
                WHERE id = NEW.condominium_membership_id;
              PERFORM pg_advisory_xact_lock(hashtextextended(v_user::text, 9182));
              IF EXISTS (
                SELECT 1
                FROM condominium_membership_roles r
                JOIN condominium_memberships m ON m.id = r.condominium_membership_id
                WHERE r.role = 3
                  AND r.is_active
                  AND r.revoked_at IS NULL
                  AND m.is_active
                  AND m.ended_at IS NULL
                  AND r.id <> NEW.id
                  AND m.user_id = v_user
              ) THEN
                RAISE EXCEPTION 'Only one active submanager link is allowed per user'
                  USING ERRCODE = '23505';
              END IF;
              RETURN NEW;
            END $fn$;
            CREATE TRIGGER trg_single_active_submanager_role
              BEFORE INSERT OR UPDATE ON condominium_membership_roles
              FOR EACH ROW EXECUTE FUNCTION enforce_single_active_submanager_role();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS trg_single_active_submanager_role ON condominium_membership_roles; DROP FUNCTION IF EXISTS enforce_single_active_submanager_role();");
    }
}
