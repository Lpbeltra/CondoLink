using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CondoLink.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ExpandRegistrationLot2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_management_companies_document",
                table: "management_companies");

            migrationBuilder.DropColumn(
                name: "phone_number",
                table: "condominiums");

            migrationBuilder.RenameColumn(
                name: "document",
                table: "management_companies",
                newName: "cnpj");

            migrationBuilder.Sql(
                "UPDATE management_companies SET cnpj = regexp_replace(cnpj, '\\D', '', 'g') WHERE cnpj IS NOT NULL AND length(regexp_replace(cnpj, '\\D', '', 'g')) = 14;");

            migrationBuilder.DropColumn(
                name: "legal_name",
                table: "management_companies");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cnpj",
                table: "users",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cpf",
                table: "users",
                type: "character varying(11)",
                maxLength: 11,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "users",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "job_title",
                table: "management_company_employees",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Não informado");

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "management_companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "management_companies",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "management_companies",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "condominiums",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "condominiums",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cnpj",
                table: "condominiums",
                type: "character varying(14)",
                maxLength: 14,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "doorman_contact",
                table: "condominiums",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "has_doorman",
                table: "condominiums",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "is_remote_doorman",
                table: "condominiums",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "state",
                table: "condominiums",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_users_manager_cnpj",
                table: "users",
                column: "cnpj",
                unique: true,
                filter: "\"cnpj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_users_manager_cpf",
                table: "users",
                column: "cpf",
                unique: true,
                filter: "\"cpf\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_management_companies_cnpj",
                table: "management_companies",
                column: "cnpj",
                unique: true,
                filter: "\"cnpj\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_condominiums_cnpj",
                table: "condominiums",
                column: "cnpj",
                unique: true,
                filter: "\"cnpj\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_users_manager_cnpj",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ux_users_manager_cpf",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ux_management_companies_cnpj",
                table: "management_companies");

            migrationBuilder.DropIndex(
                name: "ux_condominiums_cnpj",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "address",
                table: "users");

            migrationBuilder.DropColumn(
                name: "city",
                table: "users");

            migrationBuilder.DropColumn(
                name: "cnpj",
                table: "users");

            migrationBuilder.DropColumn(
                name: "cpf",
                table: "users");

            migrationBuilder.DropColumn(
                name: "state",
                table: "users");

            migrationBuilder.DropColumn(
                name: "job_title",
                table: "management_company_employees");

            migrationBuilder.DropColumn(
                name: "address",
                table: "management_companies");

            migrationBuilder.DropColumn(
                name: "city",
                table: "management_companies");

            migrationBuilder.DropColumn(
                name: "state",
                table: "management_companies");

            migrationBuilder.DropColumn(
                name: "address",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "city",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "cnpj",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "doorman_contact",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "has_doorman",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "is_remote_doorman",
                table: "condominiums");

            migrationBuilder.DropColumn(
                name: "state",
                table: "condominiums");

            migrationBuilder.RenameColumn(
                name: "cnpj",
                table: "management_companies",
                newName: "document");

            migrationBuilder.AddColumn<string>(
                name: "legal_name",
                table: "management_companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "condominiums",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_management_companies_document",
                table: "management_companies",
                column: "document",
                unique: true,
                filter: "\"document\" IS NOT NULL");
        }
    }
}
