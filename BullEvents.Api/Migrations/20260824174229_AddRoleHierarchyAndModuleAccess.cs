using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleHierarchyAndModuleAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ManagerId",
                table: "Users",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "UserModuleGrants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Module = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Granted = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GrantedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserModuleGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserModuleGrants_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ManagerId",
                table: "Users",
                column: "ManagerId");

            migrationBuilder.CreateIndex(
                name: "IX_UserModuleGrants_UserId_Module",
                table: "UserModuleGrants",
                columns: new[] { "UserId", "Module" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users",
                column: "ManagerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // The role vocabulary changed with this release. Rows still
            // carrying the old spellings are mapped to the seats that replaced
            // them, so nobody's access changes silently as a side effect of a
            // rename — BranchManager and AGM are the same team-scoped seat.
            migrationBuilder.Sql("""
                UPDATE "Users" SET "Role" = 'AGM'            WHERE "Role" = 'BranchManager';
                UPDATE "Users" SET "Role" = 'SalesManager'   WHERE "Role" = 'TeamLead';
                UPDATE "Users" SET "Role" = 'SalesExecutive' WHERE "Role" = 'SalesAgent';
                UPDATE "Users" SET "Role" = 'BackOffice'     WHERE "Role" = 'AccountsFinance';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_ManagerId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "UserModuleGrants");

            migrationBuilder.DropIndex(
                name: "IX_Users_ManagerId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ManagerId",
                table: "Users");
        }
    }
}
