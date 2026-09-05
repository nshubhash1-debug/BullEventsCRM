using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCustomisation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomFields",
                table: "Opportunities",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "CustomFields",
                table: "Leads",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "CustomFields",
                table: "Contacts",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.CreateTable(
                name: "CustomFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Object = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "crm_ci"),
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false, collation: "crm_ci"),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "crm_ci"),
                    HelpText = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    OptionsCsv = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Required = table.Column<bool>(type: "boolean", nullable: false),
                    ShowInList = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PickListValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    List = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false, collation: "crm_ci"),
                    Value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false, collation: "crm_ci"),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PickListValues", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_CompanyId_Object_Key",
                table: "CustomFieldDefinitions",
                columns: new[] { "CompanyId", "Object", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PickListValues_CompanyId_List_Value",
                table: "PickListValues",
                columns: new[] { "CompanyId", "List", "Value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CustomFieldDefinitions");

            migrationBuilder.DropTable(
                name: "PickListValues");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "CustomFields",
                table: "Contacts");
        }
    }
}
