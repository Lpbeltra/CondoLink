using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceProviderSpecialties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_provider_specialties",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_provider_specialties", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_provider_specialties_service_providers_service_prov~",
                        column: x => x.service_provider_id,
                        principalTable: "service_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_provider_specialties_normalized_name",
                table: "service_provider_specialties",
                column: "normalized_name");

            migrationBuilder.CreateIndex(
                name: "IX_service_provider_specialties_service_provider_id_normalized~",
                table: "service_provider_specialties",
                columns: new[] { "service_provider_id", "normalized_name" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO service_provider_specialties (id, service_provider_id, name, normalized_name)
                SELECT
                    (substr(md5(id::text || ':service-provider-specialty'), 1, 8) || '-' ||
                     substr(md5(id::text || ':service-provider-specialty'), 9, 4) || '-' ||
                     substr(md5(id::text || ':service-provider-specialty'), 13, 4) || '-' ||
                     substr(md5(id::text || ':service-provider-specialty'), 17, 4) || '-' ||
                     substr(md5(id::text || ':service-provider-specialty'), 21, 12))::uuid,
                    id,
                    btrim(specialty),
                    upper(btrim(specialty))
                FROM service_providers
                WHERE specialty IS NOT NULL AND btrim(specialty) <> ''
                ON CONFLICT (service_provider_id, normalized_name) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_provider_specialties");
        }
    }
}
