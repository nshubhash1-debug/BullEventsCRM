using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPermissionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ObjectVisibilityRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    GrantAccessUsingHierarchy = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectVisibilityRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PermissionSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsProfile = table.Column<bool>(type: "boolean", nullable: false),
                    RoleKey = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsSystem = table.Column<bool>(type: "boolean", nullable: false),
                    ModulesCsv = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PermissionSets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SharingRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    OwnerRoleKey = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaField = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaOperator = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaValue = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Target = table.Column<int>(type: "integer", nullable: false),
                    TargetRoleKey = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    TargetBranchId = table.Column<int>(type: "integer", nullable: true),
                    TargetUserId = table.Column<int>(type: "integer", nullable: true),
                    GrantEdit = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SharingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FieldPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PermissionSetId = table.Column<int>(type: "integer", nullable: false),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Field = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CanRead = table.Column<bool>(type: "boolean", nullable: false),
                    CanEdit = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FieldPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FieldPermissions_PermissionSets_PermissionSetId",
                        column: x => x.PermissionSetId,
                        principalTable: "PermissionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LoginPolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    PermissionSetId = table.Column<int>(type: "integer", nullable: false),
                    AllowedIpRanges = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LoginFromMinute = table.Column<int>(type: "integer", nullable: true),
                    LoginToMinute = table.Column<int>(type: "integer", nullable: true),
                    AllowedDays = table.Column<int>(type: "integer", nullable: false),
                    IdleTimeoutMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoginPolicies_PermissionSets_PermissionSetId",
                        column: x => x.PermissionSetId,
                        principalTable: "PermissionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ObjectPermissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PermissionSetId = table.Column<int>(type: "integer", nullable: false),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Actions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ObjectPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ObjectPermissions_PermissionSets_PermissionSetId",
                        column: x => x.PermissionSetId,
                        principalTable: "PermissionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserPermissionSets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    PermissionSetId = table.Column<int>(type: "integer", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    GrantedById = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserPermissionSets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserPermissionSets_PermissionSets_PermissionSetId",
                        column: x => x.PermissionSetId,
                        principalTable: "PermissionSets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserPermissionSets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FieldPermissions_PermissionSetId_Object_Field",
                table: "FieldPermissions",
                columns: new[] { "PermissionSetId", "Object", "Field" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoginPolicies_PermissionSetId",
                table: "LoginPolicies",
                column: "PermissionSetId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ObjectPermissions_PermissionSetId_Object",
                table: "ObjectPermissions",
                columns: new[] { "PermissionSetId", "Object" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ObjectVisibilityRules_CompanyId_Object",
                table: "ObjectVisibilityRules",
                columns: new[] { "CompanyId", "Object" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionSets_CompanyId_Name",
                table: "PermissionSets",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PermissionSets_CompanyId_RoleKey",
                table: "PermissionSets",
                columns: new[] { "CompanyId", "RoleKey" },
                unique: true,
                filter: "\"IsProfile\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_SharingRules_CompanyId_Object_IsActive",
                table: "SharingRules",
                columns: new[] { "CompanyId", "Object", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionSets_PermissionSetId",
                table: "UserPermissionSets",
                column: "PermissionSetId");

            migrationBuilder.CreateIndex(
                name: "IX_UserPermissionSets_UserId_PermissionSetId",
                table: "UserPermissionSets",
                columns: new[] { "UserId", "PermissionSetId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FieldPermissions");

            migrationBuilder.DropTable(
                name: "LoginPolicies");

            migrationBuilder.DropTable(
                name: "ObjectPermissions");

            migrationBuilder.DropTable(
                name: "ObjectVisibilityRules");

            migrationBuilder.DropTable(
                name: "SharingRules");

            migrationBuilder.DropTable(
                name: "UserPermissionSets");

            migrationBuilder.DropTable(
                name: "PermissionSets");
        }
    }
}
