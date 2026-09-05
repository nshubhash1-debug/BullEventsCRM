using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrationModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalProcesses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CriteriaField = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaOperator = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaValue = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LockRecord = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalProcesses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AssignmentRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CriteriaField = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaOperator = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaValue = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Strategy = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PoolUserIds = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PoolRoleKey = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    FixedUserId = table.Column<int>(type: "integer", nullable: true),
                    RoundRobinCursor = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssignmentRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessHours",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    BranchId = table.Column<int>(type: "integer", nullable: true),
                    TimeZoneId = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    SundayOpen = table.Column<int>(type: "integer", nullable: false),
                    SundayClose = table.Column<int>(type: "integer", nullable: false),
                    MondayOpen = table.Column<int>(type: "integer", nullable: false),
                    MondayClose = table.Column<int>(type: "integer", nullable: false),
                    TuesdayOpen = table.Column<int>(type: "integer", nullable: false),
                    TuesdayClose = table.Column<int>(type: "integer", nullable: false),
                    WednesdayOpen = table.Column<int>(type: "integer", nullable: false),
                    WednesdayClose = table.Column<int>(type: "integer", nullable: false),
                    ThursdayOpen = table.Column<int>(type: "integer", nullable: false),
                    ThursdayClose = table.Column<int>(type: "integer", nullable: false),
                    FridayOpen = table.Column<int>(type: "integer", nullable: false),
                    FridayClose = table.Column<int>(type: "integer", nullable: false),
                    SaturdayOpen = table.Column<int>(type: "integer", nullable: false),
                    SaturdayClose = table.Column<int>(type: "integer", nullable: false),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessHours", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DuplicateRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    MatchFields = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Action = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    AcrossOwners = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DuplicateRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ScheduledJobs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Kind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Cron = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    LastRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NextRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastOutcome = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LastMessage = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LastDurationMs = table.Column<int>(type: "integer", nullable: false),
                    RunCount = table.Column<int>(type: "integer", nullable: false),
                    FailureCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ApprovalSteps",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ApprovalProcessId = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ApproverKind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ApproverRoleKey = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ApproverUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApprovalSteps_ApprovalProcesses_ApprovalProcessId",
                        column: x => x.ApprovalProcessId,
                        principalTable: "ApprovalProcesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EscalationRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CriteriaField = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaOperator = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CriteriaValue = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    StartsFrom = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    TargetMinutes = table.Column<int>(type: "integer", nullable: false),
                    BusinessHoursId = table.Column<int>(type: "integer", nullable: true),
                    Action = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ReassignToUserId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationRules_BusinessHours_BusinessHoursId",
                        column: x => x.BusinessHoursId,
                        principalTable: "BusinessHours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Holidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    BusinessHoursId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    IsRecurring = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holidays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holidays_BusinessHours_BusinessHoursId",
                        column: x => x.BusinessHoursId,
                        principalTable: "BusinessHours",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EscalationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EscalationRuleId = table.Column<int>(type: "integer", nullable: false),
                    Object = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    RecordId = table.Column<int>(type: "integer", nullable: false),
                    BreachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Action = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Outcome = table.Column<string>(type: "text", nullable: true, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EscalationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EscalationEvents_EscalationRules_EscalationRuleId",
                        column: x => x.EscalationRuleId,
                        principalTable: "EscalationRules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalProcesses_CompanyId_Object_IsActive",
                table: "ApprovalProcesses",
                columns: new[] { "CompanyId", "Object", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_ApprovalProcessId_SortOrder",
                table: "ApprovalSteps",
                columns: new[] { "ApprovalProcessId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentRules_CompanyId_Object_IsActive_SortOrder",
                table: "AssignmentRules",
                columns: new[] { "CompanyId", "Object", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessHours_CompanyId_IsDefault",
                table: "BusinessHours",
                columns: new[] { "CompanyId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_DuplicateRules_CompanyId_Object_IsActive",
                table: "DuplicateRules",
                columns: new[] { "CompanyId", "Object", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_EscalationEvents_EscalationRuleId_Object_RecordId",
                table: "EscalationEvents",
                columns: new[] { "EscalationRuleId", "Object", "RecordId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_BusinessHoursId",
                table: "EscalationRules",
                column: "BusinessHoursId");

            migrationBuilder.CreateIndex(
                name: "IX_EscalationRules_CompanyId_Object_IsActive",
                table: "EscalationRules",
                columns: new[] { "CompanyId", "Object", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_Holidays_BusinessHoursId_Date",
                table: "Holidays",
                columns: new[] { "BusinessHoursId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledJobs_CompanyId_IsActive_NextRunAt",
                table: "ScheduledJobs",
                columns: new[] { "CompanyId", "IsActive", "NextRunAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalSteps");

            migrationBuilder.DropTable(
                name: "AssignmentRules");

            migrationBuilder.DropTable(
                name: "DuplicateRules");

            migrationBuilder.DropTable(
                name: "EscalationEvents");

            migrationBuilder.DropTable(
                name: "Holidays");

            migrationBuilder.DropTable(
                name: "ScheduledJobs");

            migrationBuilder.DropTable(
                name: "ApprovalProcesses");

            migrationBuilder.DropTable(
                name: "EscalationRules");

            migrationBuilder.DropTable(
                name: "BusinessHours");
        }
    }
}
