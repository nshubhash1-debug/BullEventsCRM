using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class OrganisationAndCompanyProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DesignationId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmployeeCode",
                table: "Users",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<DateTime>(
                name: "JoinedOn",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddressLine1",
                table: "Companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "AddressLine2",
                table: "Companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Cin",
                table: "Companies",
                type: "character varying(21)",
                maxLength: 21,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "City",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "CurrencyCode",
                table: "Companies",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "",
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Companies",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "FinancialYearStartMonth",
                table: "Companies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LegalName",
                table: "Companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "LetterheadFooter",
                table: "Companies",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Pan",
                table: "Companies",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Companies",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Pincode",
                table: "Companies",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "ReraNumber",
                table: "Companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "State",
                table: "Companies",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "SupportEmail",
                table: "Companies",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Tan",
                table: "Companies",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                table: "Companies",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "",
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Companies",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.CreateTable(
                name: "Designations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true, collation: "crm_ci"),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SuggestedRole = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Designations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, collation: "crm_ci"),
                    Code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true, collation: "crm_ci"),
                    Kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, collation: "crm_ci"),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    LeadUserId = table.Column<int>(type: "integer", nullable: true),
                    ParentTeamId = table.Column<int>(type: "integer", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Teams_Teams_ParentTeamId",
                        column: x => x.ParentTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Teams_Users_LeadUserId",
                        column: x => x.LeadUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    RoleInTeam = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false, collation: "crm_ci"),
                    JoinedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LeftOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamTransfers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    FromTeamId = table.Column<int>(type: "integer", nullable: true),
                    ToTeamId = table.Column<int>(type: "integer", nullable: true),
                    EffectiveOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    MovedByUserId = table.Column<int>(type: "integer", nullable: true),
                    MovedByName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamTransfers_Teams_FromTeamId",
                        column: x => x.FromTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeamTransfers_Teams_ToTeamId",
                        column: x => x.ToTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeamTransfers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DesignationId",
                table: "Users",
                column: "DesignationId");

            migrationBuilder.CreateIndex(
                name: "IX_Designations_CompanyId_Level",
                table: "Designations",
                columns: new[] { "CompanyId", "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Designations_CompanyId_Name",
                table: "Designations",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_CompanyId_UserId_LeftOn",
                table: "TeamMembers",
                columns: new[] { "CompanyId", "UserId", "LeftOn" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId_UserId",
                table: "TeamMembers",
                columns: new[] { "TeamId", "UserId" },
                unique: true,
                filter: "\"LeftOn\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_UserId",
                table: "TeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_BranchId",
                table: "Teams",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CompanyId_Kind_IsActive",
                table: "Teams",
                columns: new[] { "CompanyId", "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CompanyId_Name",
                table: "Teams",
                columns: new[] { "CompanyId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_LeadUserId",
                table: "Teams",
                column: "LeadUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ParentTeamId",
                table: "Teams",
                column: "ParentTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTransfers_CompanyId_EffectiveOn",
                table: "TeamTransfers",
                columns: new[] { "CompanyId", "EffectiveOn" });

            migrationBuilder.CreateIndex(
                name: "IX_TeamTransfers_FromTeamId",
                table: "TeamTransfers",
                column: "FromTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTransfers_ToTeamId",
                table: "TeamTransfers",
                column: "ToTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTransfers_UserId",
                table: "TeamTransfers",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Designations_DesignationId",
                table: "Users",
                column: "DesignationId",
                principalTable: "Designations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Designations_DesignationId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "Designations");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "TeamTransfers");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_Users_DesignationId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DesignationId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "EmployeeCode",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "JoinedOn",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AddressLine1",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "AddressLine2",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Cin",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "City",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "CurrencyCode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "FinancialYearStartMonth",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LegalName",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "LetterheadFooter",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Pan",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Pincode",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "ReraNumber",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "State",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "SupportEmail",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Tan",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Companies");
        }
    }
}
