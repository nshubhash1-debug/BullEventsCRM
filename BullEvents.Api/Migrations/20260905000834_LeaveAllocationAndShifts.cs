using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BullEvents.Api.Migrations
{
    /// <inheritdoc />
    public partial class LeaveAllocationAndShifts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowEncashment",
                table: "HrLeaveTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AllowNegativeBalance",
                table: "HrLeaveTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CompensatoryExpiryDays",
                table: "HrLeaveTypes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsCompensatory",
                table: "HrLeaveTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxBalance",
                table: "HrLeaveTypes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxCarryForward",
                table: "HrLeaveTypes",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "HrAttendanceRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RequestedStatus = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    HalfDay = table.Column<bool>(type: "boolean", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    AttachmentUrl = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrAttendanceRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrAttendanceRequests_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrCompensatoryRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    WorkedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "integer", nullable: false),
                    Days = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    AllocationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrCompensatoryRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrCompensatoryRequests_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrCompensatoryRequests_HrLeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "HrLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrLeaveBlockDates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    DepartmentId = table.Column<int>(type: "integer", nullable: true),
                    AllowOverride = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrLeaveBlockDates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrLeaveBlockDates_HrDepartments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "HrDepartments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "HrLeavePeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RolledOverAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrLeavePeriods", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrLeavePolicies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrLeavePolicies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HrShiftAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ShiftId = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LeadId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrShiftAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrShiftAssignments_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrShiftAssignments_HrShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "HrShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrShiftRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    ShiftId = table.Column<int>(type: "integer", nullable: false),
                    FromDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ToDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    ShiftAssignmentId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrShiftRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrShiftRequests_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrShiftRequests_HrShifts_ShiftId",
                        column: x => x.ShiftId,
                        principalTable: "HrShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrLeaveAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "integer", nullable: false),
                    LeavePeriodId = table.Column<int>(type: "integer", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    Days = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    SourceRecordId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true, collation: "crm_ci"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrLeaveAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrLeaveAllocations_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrLeaveAllocations_HrLeavePeriods_LeavePeriodId",
                        column: x => x.LeavePeriodId,
                        principalTable: "HrLeavePeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrLeaveAllocations_HrLeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "HrLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrLeaveEncashments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "integer", nullable: false),
                    LeavePeriodId = table.Column<int>(type: "integer", nullable: false),
                    Days = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PerDayAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, collation: "crm_ci"),
                    PaidInPayrollRunId = table.Column<int>(type: "integer", nullable: true),
                    AllocationId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrLeaveEncashments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrLeaveEncashments_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrLeaveEncashments_HrLeavePeriods_LeavePeriodId",
                        column: x => x.LeavePeriodId,
                        principalTable: "HrLeavePeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrLeaveEncashments_HrLeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "HrLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrLeavePolicyAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    EmployeeId = table.Column<int>(type: "integer", nullable: false),
                    LeavePolicyId = table.Column<int>(type: "integer", nullable: false),
                    LeavePeriodId = table.Column<int>(type: "integer", nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    AppliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DaysAllocated = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    UpdatedById = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrLeavePolicyAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrLeavePolicyAssignments_HrEmployees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "HrEmployees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrLeavePolicyAssignments_HrLeavePeriods_LeavePeriodId",
                        column: x => x.LeavePeriodId,
                        principalTable: "HrLeavePeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrLeavePolicyAssignments_HrLeavePolicies_LeavePolicyId",
                        column: x => x.LeavePolicyId,
                        principalTable: "HrLeavePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HrLeavePolicyLines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyId = table.Column<int>(type: "integer", nullable: false),
                    LeavePolicyId = table.Column<int>(type: "integer", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "integer", nullable: false),
                    AnnualAllocation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrLeavePolicyLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrLeavePolicyLines_HrLeavePolicies_LeavePolicyId",
                        column: x => x.LeavePolicyId,
                        principalTable: "HrLeavePolicies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HrLeavePolicyLines_HrLeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "HrLeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HrAttendanceRequests_EmployeeId_FromDate_ToDate",
                table: "HrAttendanceRequests",
                columns: new[] { "EmployeeId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HrCompensatoryRequests_EmployeeId_WorkedOn",
                table: "HrCompensatoryRequests",
                columns: new[] { "EmployeeId", "WorkedOn" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrCompensatoryRequests_LeaveTypeId",
                table: "HrCompensatoryRequests",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeaveAllocations_EmployeeId_LeavePeriodId_LeaveTypeId",
                table: "HrLeaveAllocations",
                columns: new[] { "EmployeeId", "LeavePeriodId", "LeaveTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_HrLeaveAllocations_LeavePeriodId",
                table: "HrLeaveAllocations",
                column: "LeavePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeaveAllocations_LeaveTypeId",
                table: "HrLeaveAllocations",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeaveBlockDates_CompanyId_FromDate_ToDate",
                table: "HrLeaveBlockDates",
                columns: new[] { "CompanyId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HrLeaveBlockDates_DepartmentId",
                table: "HrLeaveBlockDates",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeaveEncashments_EmployeeId",
                table: "HrLeaveEncashments",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeaveEncashments_LeavePeriodId",
                table: "HrLeaveEncashments",
                column: "LeavePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeaveEncashments_LeaveTypeId",
                table: "HrLeaveEncashments",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeavePeriods_CompanyId_FromDate_ToDate",
                table: "HrLeavePeriods",
                columns: new[] { "CompanyId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HrLeavePolicyAssignments_EmployeeId_LeavePeriodId",
                table: "HrLeavePolicyAssignments",
                columns: new[] { "EmployeeId", "LeavePeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrLeavePolicyAssignments_LeavePeriodId",
                table: "HrLeavePolicyAssignments",
                column: "LeavePeriodId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeavePolicyAssignments_LeavePolicyId",
                table: "HrLeavePolicyAssignments",
                column: "LeavePolicyId");

            migrationBuilder.CreateIndex(
                name: "IX_HrLeavePolicyLines_LeavePolicyId_LeaveTypeId",
                table: "HrLeavePolicyLines",
                columns: new[] { "LeavePolicyId", "LeaveTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrLeavePolicyLines_LeaveTypeId",
                table: "HrLeavePolicyLines",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_HrShiftAssignments_EmployeeId_FromDate_ToDate",
                table: "HrShiftAssignments",
                columns: new[] { "EmployeeId", "FromDate", "ToDate" });

            migrationBuilder.CreateIndex(
                name: "IX_HrShiftAssignments_ShiftId",
                table: "HrShiftAssignments",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_HrShiftRequests_EmployeeId_Status",
                table: "HrShiftRequests",
                columns: new[] { "EmployeeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HrShiftRequests_ShiftId",
                table: "HrShiftRequests",
                column: "ShiftId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HrAttendanceRequests");

            migrationBuilder.DropTable(
                name: "HrCompensatoryRequests");

            migrationBuilder.DropTable(
                name: "HrLeaveAllocations");

            migrationBuilder.DropTable(
                name: "HrLeaveBlockDates");

            migrationBuilder.DropTable(
                name: "HrLeaveEncashments");

            migrationBuilder.DropTable(
                name: "HrLeavePolicyAssignments");

            migrationBuilder.DropTable(
                name: "HrLeavePolicyLines");

            migrationBuilder.DropTable(
                name: "HrShiftAssignments");

            migrationBuilder.DropTable(
                name: "HrShiftRequests");

            migrationBuilder.DropTable(
                name: "HrLeavePeriods");

            migrationBuilder.DropTable(
                name: "HrLeavePolicies");

            migrationBuilder.DropColumn(
                name: "AllowEncashment",
                table: "HrLeaveTypes");

            migrationBuilder.DropColumn(
                name: "AllowNegativeBalance",
                table: "HrLeaveTypes");

            migrationBuilder.DropColumn(
                name: "CompensatoryExpiryDays",
                table: "HrLeaveTypes");

            migrationBuilder.DropColumn(
                name: "IsCompensatory",
                table: "HrLeaveTypes");

            migrationBuilder.DropColumn(
                name: "MaxBalance",
                table: "HrLeaveTypes");

            migrationBuilder.DropColumn(
                name: "MaxCarryForward",
                table: "HrLeaveTypes");
        }
    }
}
