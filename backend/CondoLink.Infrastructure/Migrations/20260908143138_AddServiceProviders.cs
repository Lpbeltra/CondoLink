using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_providers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    company_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    specialty = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    contact_name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    phone = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    pix_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    pix_key_type = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_providers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "service_provider_condominium_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    condominium_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_provider_condominium_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_provider_condominium_links_condominiums_condominium~",
                        column: x => x.condominium_id,
                        principalTable: "condominiums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_provider_condominium_links_service_providers_servic~",
                        column: x => x.service_provider_id,
                        principalTable: "service_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "service_provider_user_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_provider_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_provider_user_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_provider_user_links_service_providers_service_provi~",
                        column: x => x.service_provider_id,
                        principalTable: "service_providers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_service_provider_user_links_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_provider_condominium_links_condominium_id",
                table: "service_provider_condominium_links",
                column: "condominium_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_provider_condominium_links_service_provider_id_cond~",
                table: "service_provider_condominium_links",
                columns: new[] { "service_provider_id", "condominium_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_provider_user_links_service_provider_id_user_id",
                table: "service_provider_user_links",
                columns: new[] { "service_provider_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_service_provider_user_links_user_id",
                table: "service_provider_user_links",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_providers_name",
                table: "service_providers",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_provider_condominium_links");

            migrationBuilder.DropTable(
                name: "service_provider_user_links");

            migrationBuilder.DropTable(
                name: "service_providers");
        }
    }
}
