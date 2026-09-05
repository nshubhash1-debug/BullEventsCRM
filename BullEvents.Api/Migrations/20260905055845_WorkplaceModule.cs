using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class WorkplaceModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ChecklistTaskId",
                table: "HrOnboardingItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "HrOnboardingItems",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DueOn",
                table: "HrOnboardingItems",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBlocking",
                table: "HrOnboardingItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "HrOnboardingItems",
                type: "text",
                nullable: false,
                defaultValue: "",
                collation: "crm_ci");

            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "HrOnboardingItems",
                type: "text",
                nullable: false,
                defaultValue: "",
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "HrOnboardingItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedAmount",
                table: "HrExpenseClaims",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExpenseClaimTypeId",
                table: "HrExpenseClaims",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReductionReason",
                table: "HrExpenseClaims",
                type: "text",
                nullable: true,
                collation: "crm_ci");

            migrationBuilder.AddColumn<int>(
                name: "TravelRequestId",
                table: "HrExpenseClaims",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HrChecklistTemplates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Kind = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    CollarType = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrChecklistTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrChecklistTemplates_HrDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "HrDepartments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HrExpenseClaimTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    PerClaimLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    MonthlyLimit = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiresReceipt = table.Column<bool>(type: "boolean", nullable: false),
                    ReceiptWaivedBelow = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    RequiresTravelRequest = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrExpenseClaimTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrGrievances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Subject = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Details = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    AgainstEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    AssignedToEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    IsConfidential = table.Column<bool>(type: "boolean", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Resolution = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    ComplainantSatisfied = table.Column<bool>(type: "boolean", nullable: true),
                    AttachmentUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrGrievances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrGrievances_HrEmployees_AssignedToEmployeeId",
                        column: x => x.AssignedToEmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HrGrievances_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HrSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Category = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    RequiresCertification = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrSkills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrTimesheets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    WeekStarting = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    TotalHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    BillableHours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecisionNote = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTimesheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrTimesheets_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrTravelRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    Purpose = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Destination = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvanceRequested = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AdvancePaid = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DecisionNote = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTravelRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrTravelRequests_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrChecklistTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    ChecklistTemplateId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Description = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Owner = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DueOffsetDays = table.Column<int>(type: "integer", nullable: false),
                    IsBlocking = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrChecklistTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrChecklistTasks_HrChecklistTemplates_ChecklistTemplateId",
                        column: x => x.ChecklistTemplateId,
                        principalTable: "HrChecklistTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrEmployeeSkills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    Proficiency = table.Column<int>(type: "integer", nullable: false),
                    YearsOfExperience = table.Column<int>(type: "integer", nullable: false),
                    AssessedByEmployeeId = table.Column<int>(type: "integer", nullable: true),
                    AssessedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CertificateNumber = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CertifiedUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrEmployeeSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrEmployeeSkills_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrEmployeeSkills_HrSkills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "HrSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrTimesheetLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TimesheetId = table.Column<int>(type: "integer", nullable: false),
                    OnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    Activity = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Hours = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IsBillable = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTimesheetLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrTimesheetLines_HrTimesheets_TimesheetId",
                        column: x => x.TimesheetId,
                        principalTable: "HrTimesheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrTravelLegs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    TravelRequestId = table.Column<int>(type: "integer", nullable: false),
                    OnDate = table.Column<DateOnly>(type: "date", nullable: false),
                    From = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    To = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Mode = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    EstimatedCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrTravelLegs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrTravelLegs_HrTravelRequests_TravelRequestId",
                        column: x => x.TravelRequestId,
                        principalTable: "HrTravelRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrExpenseClaims_ExpenseClaimTypeId",
                table: "HrExpenseClaims",
                column: "ExpenseClaimTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExpenseClaims_TravelRequestId",
                table: "HrExpenseClaims",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_HrChecklistTasks_ChecklistTemplateId_Title",
                table: "HrChecklistTasks",
                columns: new[] { "ChecklistTemplateId", "Title" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrChecklistTemplates_CompanyId_Kind_IsActive",
                table: "HrChecklistTemplates",
                columns: new[] { "CompanyId", "Kind", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_HrChecklistTemplates_DepartmentId",
                table: "HrChecklistTemplates",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployeeSkills_EmployeeId_SkillId",
                table: "HrEmployeeSkills",
                columns: new[] { "EmployeeId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrEmployeeSkills_SkillId",
                table: "HrEmployeeSkills",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_HrExpenseClaimTypes_CompanyId_Name",
                table: "HrExpenseClaimTypes",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrGrievances_AssignedToEmployeeId",
                table: "HrGrievances",
                column: "AssignedToEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrGrievances_CompanyId_Status",
                table: "HrGrievances",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HrGrievances_EmployeeId",
                table: "HrGrievances",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrSkills_CompanyId_Name",
                table: "HrSkills",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrTimesheetLines_TimesheetId_OnDate",
                table: "HrTimesheetLines",
                columns: new[] { "TimesheetId", "OnDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HrTimesheets_EmployeeId_WeekStarting",
                table: "HrTimesheets",
                columns: new[] { "EmployeeId", "WeekStarting" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrTravelLegs_TravelRequestId",
                table: "HrTravelLegs",
                column: "TravelRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_HrTravelRequests_EmployeeId_FromDate_ToDate",
                table: "HrTravelRequests",
                columns: new[] { "EmployeeId", "FromDate", "ToDate" });

            migrationBuilder.AddForeignKey(
                name: "FK_HrExpenseClaims_HrExpenseClaimTypes_ExpenseClaimTypeId",
                table: "HrExpenseClaims",
                column: "ExpenseClaimTypeId",
                principalTable: "HrExpenseClaimTypes",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_HrExpenseClaims_HrTravelRequests_TravelRequestId",
                table: "HrExpenseClaims",
                column: "TravelRequestId",
                principalTable: "HrTravelRequests",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HrExpenseClaims_HrExpenseClaimTypes_ExpenseClaimTypeId",
                table: "HrExpenseClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_HrExpenseClaims_HrTravelRequests_TravelRequestId",
                table: "HrExpenseClaims");

            migrationBuilder.DropTable(
                name: "HrChecklistTasks");

            migrationBuilder.DropTable(
                name: "HrEmployeeSkills");

            migrationBuilder.DropTable(
                name: "HrExpenseClaimTypes");

            migrationBuilder.DropTable(
                name: "HrGrievances");

            migrationBuilder.DropTable(
                name: "HrTimesheetLines");

            migrationBuilder.DropTable(
                name: "HrTravelLegs");

            migrationBuilder.DropTable(
                name: "HrChecklistTemplates");

            migrationBuilder.DropTable(
                name: "HrSkills");

            migrationBuilder.DropTable(
                name: "HrTimesheets");

            migrationBuilder.DropTable(
                name: "HrTravelRequests");

            migrationBuilder.DropIndex(
                name: "IX_HrExpenseClaims_ExpenseClaimTypeId",
                table: "HrExpenseClaims");

            migrationBuilder.DropIndex(
                name: "IX_HrExpenseClaims_TravelRequestId",
                table: "HrExpenseClaims");

            migrationBuilder.DropColumn(
                name: "ChecklistTaskId",
                table: "HrOnboardingItems");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "HrOnboardingItems");

            migrationBuilder.DropColumn(
                name: "DueOn",
                table: "HrOnboardingItems");

            migrationBuilder.DropColumn(
                name: "IsBlocking",
                table: "HrOnboardingItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "HrOnboardingItems");

            migrationBuilder.DropColumn(
                name: "Owner",
                table: "HrOnboardingItems");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "HrOnboardingItems");

            migrationBuilder.DropColumn(
                name: "ApprovedAmount",
                table: "HrExpenseClaims");

            migrationBuilder.DropColumn(
                name: "ExpenseClaimTypeId",
                table: "HrExpenseClaims");

            migrationBuilder.DropColumn(
                name: "ReductionReason",
                table: "HrExpenseClaims");

            migrationBuilder.DropColumn(
                name: "TravelRequestId",
                table: "HrExpenseClaims");
        }
    }
}
